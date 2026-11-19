using Application.Common;
using Application.Servers;
using Contracts.Servers;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Application.Tests.Servers;

/// <summary>See my servers — API-SRV-03: listing and opening servers, with the repository faked.</summary>
public sealed class SeeMyServersTests
{
    private readonly FakeServerRepository _servers = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly Guid _alice = Guid.CreateVersion7();
    private readonly Guid _bob = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<ServerDetailDto> CreateAsync(Guid owner, string name, string? iconUrl = null)
    {
        _clock.Advance(TimeSpan.FromSeconds(1));
        return (await new CreateServer(_servers, _clock).HandleAsync(owner, new CreateServerRequest(name, iconUrl), Ct)).Value;
    }

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task My_list_shows_the_servers_Im_in_and_nothing_else_in_creation_order()
    {
        // Given
        var first = await CreateAsync(_alice, "First", "https://example.test/a.png");
        await CreateAsync(_bob, "Bob's");
        var second = await CreateAsync(_alice, "Second");

        // When
        var mine = await new GetMyServers(_servers).HandleAsync(_alice, Ct);

        // Then
        Assert.Equal(
            [new ServerDto(first.Id, "First", "https://example.test/a.png", _alice), new ServerDto(second.Id, "Second", null, _alice)],
            mine);
    }

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task Opening_one_of_my_servers_shows_what_creating_it_returned()
    {
        // Given
        var created = await CreateAsync(_alice, "First");

        // When
        var result = await new GetServer(_servers).HandleAsync(_alice, created.Id, Ct);

        // Then
        var server = result.Value;
        Assert.Equal((created.Id, created.Name, created.IconUrl, created.OwnerId, created.MyPermissions),
            (server.Id, server.Name, server.IconUrl, server.OwnerId, server.MyPermissions));
        Assert.Equal(created.Channels, server.Channels);
        Assert.Equal(created.Roles, server.Roles);
    }

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task A_server_Im_not_in_looks_exactly_like_one_that_doesnt_exist()
    {
        // Given
        var bobs = await CreateAsync(_bob, "Bob's");

        // When
        var notMine = await new GetServer(_servers).HandleAsync(_alice, bobs.Id, Ct);
        var missing = await new GetServer(_servers).HandleAsync(_alice, Guid.NewGuid(), Ct);

        // Then
        Assert.Equal(ErrorKind.NotFound, notMine.Error.Kind);
        Assert.Equal(ServerErrors.NotFound, notMine.Error);
        Assert.Equal(notMine.Error, missing.Error);
    }
}
