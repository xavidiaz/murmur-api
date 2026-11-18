namespace Api.AcceptanceTests.Infrastructure;

/// <summary>One server → client call received on a hub connection.</summary>
public sealed record HubEvent(string Name, object? Payload, DateTimeOffset ReceivedAt);

/// <summary>
/// Records every <see cref="IChatClient"/> call a connection receives, in arrival order.
/// <see cref="WaitForAsync{T}"/> consumes the first matching unconsumed event, so successive waits
/// observe successive events (e.g. MessageUpdated, then MessageDeleted).
/// </summary>
public sealed class EventRecorder
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private readonly List<HubEvent> _all = [];
    private readonly List<HubEvent> _pending = [];
    private TaskCompletionSource _changed = NewSignal();

    /// <summary>Everything received so far (consumed or not), in arrival order.</summary>
    public IReadOnlyList<HubEvent> All
    {
        get { lock (_gate) return _all.ToArray(); }
    }

    internal void Record(string name, object? payload)
    {
        TaskCompletionSource toSignal;
        lock (_gate)
        {
            var e = new HubEvent(name, payload, DateTimeOffset.UtcNow);
            _all.Add(e);
            _pending.Add(e);
            toSignal = _changed;
            _changed = NewSignal();
        }
        toSignal.TrySetResult();
    }

    /// <summary>
    /// Waits (default 5 s) for the first unconsumed event called <paramref name="name"/> whose payload is a
    /// <typeparamref name="T"/> matching <paramref name="match"/>; consumes and returns it. Fails the test on timeout,
    /// listing what was received.
    /// </summary>
    public async Task<T> WaitForAsync<T>(string name, Func<T, bool>? match = null, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var deadline = DateTimeOffset.UtcNow + limit;
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                var index = _pending.FindIndex(e => e.Name == name && e.Payload is T t && (match is null || match(t)));
                if (index >= 0)
                {
                    var hit = _pending[index];
                    _pending.RemoveAt(index);
                    return (T)hit.Payload!;
                }
                changed = _changed.Task;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;
            try
            {
                await changed.WaitAsync(remaining, TestContext.Current.CancellationToken);
            }
            catch (TimeoutException)
            {
                break;
            }
        }
        Assert.Fail($"No matching '{name}' event within {limit.TotalMilliseconds:0} ms. Received: {Describe()}");
        return default!; // unreachable
    }

    /// <summary>
    /// Waits <paramref name="within"/> (default 500 ms) and asserts no unconsumed event called <paramref name="name"/>
    /// (any name when null) matching <paramref name="match"/> arrived.
    /// </summary>
    public async Task AssertNoEventAsync(string? name = null, Func<object?, bool>? match = null, TimeSpan? within = null)
    {
        await Task.Delay(within ?? DefaultQuietPeriod, TestContext.Current.CancellationToken);
        lock (_gate)
        {
            var offending = _pending.Where(e => (name is null || e.Name == name) && (match is null || match(e.Payload))).ToList();
            Assert.True(offending.Count == 0,
                $"Expected no '{name ?? "*"}' event, but received: {string.Join(", ", offending.Select(e => $"{e.Name}({e.Payload})"))}");
        }
    }

    /// <summary>Typed overload of <see cref="AssertNoEventAsync(string?, Func{object?, bool}?, TimeSpan?)"/>.</summary>
    public Task AssertNoEventAsync<T>(string name, Func<T, bool> match, TimeSpan? within = null) =>
        AssertNoEventAsync(name, p => p is T t && match(t), within);

    /// <summary>Drops all unconsumed events (e.g. noise produced while arranging).</summary>
    public void Clear()
    {
        lock (_gate) _pending.Clear();
    }

    private string Describe()
    {
        lock (_gate)
            return _all.Count == 0 ? "(nothing)" : string.Join(", ", _all.Select(e => $"{e.Name}({e.Payload})"));
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
