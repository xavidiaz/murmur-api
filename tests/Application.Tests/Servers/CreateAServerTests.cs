using Application.Common;
using Application.Servers;
using Contracts.Channels;
using Contracts.Servers;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using DomainServer = Domain.Servers.Server;

namespace Application.Tests.Servers;

internal sealed class FakeServerRepository : IServerRepository
{
    public List<DomainServer> Added { get; } = [];

    public Task AddAsync(DomainServer server, CancellationToken ct)
    {
        Added.Add(server);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ServerSummary>> ListForMemberAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ServerSummary>>([.. Added
            .Where(s => s.Members.Any(m => m.UserId == userId))
            .OrderBy(s => s.Id)
            .Select(s => new ServerSummary(s.Id, s.Name, s.IconUrl, s.OwnerId))]);

    public Task<DomainServer?> FindForMemberAsync(Guid serverId, Guid userId, CancellationToken ct) =>
        Task.FromResult(Added.SingleOrDefault(s => s.Id == serverId && s.Members.Any(m => m.UserId == userId)));

    /// <summary>Access rows set directly by a test (e.g. a member whose @everyone lacks a permission).</summary>
    public Dictionary<(Guid ServerId, Guid UserId), ServerAccess> AccessOverrides { get; } = [];

    public Task<ServerAccess?> GetAccessAsync(Guid serverId, Guid userId, CancellationToken ct)
    {
        if (AccessOverrides.TryGetValue((serverId, userId), out var overridden))
            return Task.FromResult<ServerAccess?>(overridden);
        var server = Added.SingleOrDefault(s => s.Id == serverId && s.Members.Any(m => m.UserId == userId));
        return Task.FromResult(server is null ? null : new ServerAccess(server.Id, server.Name, server.IconUrl, server.OwnerId,
            server.Roles.Single(r => r.IsEveryone).Permissions, server.Members.Count));
    }
}

/// <summary>Create a server — API-SRV-02: the use case, with the repository faked.</summary>
public sealed class CreateAServerTests
{
    private static readonly Permissions AllPermissions =
        Enum.GetValues<Permissions>().Aggregate(Permissions.None, (all, p) => all | p);

    private readonly FakeServerRepository _servers = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly Guid _me = Guid.CreateVersion7();
    private CreateServer UseCase => new(_servers, _clock);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Story", "API-SRV-02")]
    public async Task Creating_a_server_makes_me_its_owner_and_gives_me_a_general_channel_and_an_everyone_role()
    {
        // When
        var result = await UseCase.HandleAsync(_me, new CreateServerRequest("  My Place ", null), Ct);

        // Then
        var server = result.Value;
        Assert.Equal("My Place", server.Name);
        Assert.Equal(_me, server.OwnerId);
        Assert.Null(server.IconUrl);
        var general = Assert.Single(server.Channels);
        Assert.Equal(new ChannelDto(general.Id, server.Id, "general", null, 0, ChannelType.Text), general);
        var everyone = Assert.Single(server.Roles);
        Assert.Equal(new RoleDto(everyone.Id, server.Id, "@everyone", null, 0,
            Permissions.ViewChannel | Permissions.SendMessages | Permissions.CreateInvite, true), everyone);
        Assert.Equal(AllPermissions, server.MyPermissions);

        var saved = Assert.Single(_servers.Added);
        Assert.Equal(server.Id, saved.Id);
        Assert.Equal(_clock.GetUtcNow(), saved.Members.Single().JoinedAt);
    }

    [Theory]
    [Trait("Story", "API-SRV-02")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_blank_name_is_rejected_and_nothing_is_saved(string? name)
    {
        var result = await UseCase.HandleAsync(_me, new CreateServerRequest(name!, null), Ct);

        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal(["name"], result.Error.Errors!.Keys);
        Assert.Empty(_servers.Added);
    }

    [Fact]
    [Trait("Story", "API-SRV-02")]
    public async Task A_name_over_100_characters_is_rejected()
    {
        var result = await UseCase.HandleAsync(_me, new CreateServerRequest(new string('n', 101), null), Ct);

        Assert.Equal(["name"], result.Error.Errors!.Keys);
        Assert.Empty(_servers.Added);
    }

    [Theory]
    [Trait("Story", "API-SRV-02")]
    [InlineData("https://example.test/icon.png")]
    [InlineData("http://example.test/icon.png")]
    public async Task An_absolute_http_or_https_icon_url_is_accepted(string iconUrl)
    {
        var result = await UseCase.HandleAsync(_me, new CreateServerRequest("My Place", iconUrl), Ct);

        Assert.Equal(iconUrl, result.Value.IconUrl);
    }

    public static TheoryData<string> BadIconUrls => new()
    {
        "icon.png",
        "/icons/icon.png",
        "ftp://example.test/icon.png",
        "javascript:alert(1)",
        "",
        "https://example.test/" + new string('a', 2048),
    };

    [Theory]
    [Trait("Story", "API-SRV-02")]
    [MemberData(nameof(BadIconUrls))]
    public async Task A_bad_icon_url_is_rejected_on_its_own(string iconUrl)
    {
        var result = await UseCase.HandleAsync(_me, new CreateServerRequest("My Place", iconUrl), Ct);

        Assert.Equal(["iconUrl"], result.Error.Errors!.Keys);
        Assert.Empty(_servers.Added);
    }
}
