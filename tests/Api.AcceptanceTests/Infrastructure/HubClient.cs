using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Api.AcceptanceTests.Infrastructure;

/// <summary>
/// A SignalR <see cref="HubConnection"/> to <see cref="ApiRoutes.Hub"/> on the in-memory TestServer
/// (LongPolling via <c>Server.CreateHandler()</c>), with every <see cref="IChatClient"/> method recorded in <see cref="Events"/>.
/// </summary>
public sealed class HubClient : IAsyncDisposable
{
    private HubClient(HubConnection connection, EventRecorder events)
    {
        Connection = connection;
        Events = events;
    }

    public HubConnection Connection { get; }
    public EventRecorder Events { get; }

    /// <summary>
    /// Builds (does not start) a connection. <paramref name="accessToken"/> null = unauthenticated
    /// (use it to assert that <c>StartAsync</c> fails with 401).
    /// </summary>
    public static HubConnection Build(ApiFactory api, string? accessToken)
    {
        var server = api.Server; // starts the host if needed
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, ApiRoutes.Hub), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                if (accessToken is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();
    }

    /// <summary>Connects with <paramref name="accessToken"/>, records all <see cref="IChatClient"/> events, and starts.</summary>
    public static async Task<HubClient> ConnectAsync(ApiFactory api, string accessToken)
    {
        var connection = Build(api, accessToken);
        var events = new EventRecorder();
        foreach (var method in typeof(IChatClient).GetMethods())
        {
            var name = method.Name;
            var types = method.GetParameters().Select(p => p.ParameterType).ToArray();
            connection.On(name, types, (args, _) =>
            {
                events.Record(name, args.Length == 1 ? args[0] : args);
                return Task.CompletedTask;
            }, state: null!);
        }
        await connection.StartAsync(TestContext.Current.CancellationToken);
        return new HubClient(connection, events);
    }

    /// <summary>Connects as <paramref name="user"/> on the user's API host.</summary>
    public static Task<HubClient> ConnectAsync(ApiClient user) => ConnectAsync(user.Api, user.AccessToken);

    // ---- IChatHub calls (a HubException surfaces from these when the server refuses) ----
    public Task SubscribeChannelAsync(Guid channelId) => InvokeAsync(nameof(IChatHub.SubscribeChannel), channelId);
    public Task UnsubscribeChannelAsync(Guid channelId) => InvokeAsync(nameof(IChatHub.UnsubscribeChannel), channelId);
    public Task TypingInChannelAsync(Guid channelId) => InvokeAsync(nameof(IChatHub.TypingInChannel), channelId);
    public Task TypingInDmAsync(Guid conversationId) => InvokeAsync(nameof(IChatHub.TypingInDm), conversationId);
    public Task HeartbeatAsync() => InvokeAsync(nameof(IChatHub.Heartbeat));
    public Task<IReadOnlyList<Guid>> GetOnlineUsersAsync() => InvokeAsync<IReadOnlyList<Guid>>(nameof(IChatHub.GetOnlineUsers));

    public Task InvokeAsync(string method, params object?[] args) =>
        Connection.InvokeCoreAsync(method, args, TestContext.Current.CancellationToken);

    public Task<T> InvokeAsync<T>(string method, params object?[] args) =>
        Connection.InvokeCoreAsync<T>(method, args, TestContext.Current.CancellationToken);

    // ---- event shortcuts ----
    public Task<T> WaitForAsync<T>(string eventName, Func<T, bool>? match = null, TimeSpan? timeout = null) =>
        Events.WaitForAsync(eventName, match, timeout);

    public Task AssertNoEventAsync(string? eventName = null, TimeSpan? within = null) =>
        Events.AssertNoEventAsync(eventName, null, within);

    public Task AssertNoEventAsync<T>(string eventName, Func<T, bool> match, TimeSpan? within = null) =>
        Events.AssertNoEventAsync(eventName, match, within);

    /// <summary>Closes the connection cleanly (the server sees the disconnect immediately).</summary>
    public Task StopAsync() => Connection.StopAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        try { await Connection.StopAsync(CancellationToken.None); }
        catch { /* already closed */ }
        await Connection.DisposeAsync();
    }
}
