namespace Api.AcceptanceTests.Servers;

/// <summary>See my servers — API-SRV-03. As a user, I want to see the servers I belong to so that I can move between them.</summary>
public sealed class SeeMyServersTests(PostgresFixture db) : AcceptanceTest(db)
{
    private static readonly Permissions AllPermissions =
        Enum.GetValues<Permissions>().Aggregate(Permissions.None, (all, p) => all | p);

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task My_list_shows_the_servers_Im_in_and_nothing_else()
    {
        // Given (until invites exist, "servers I'm in" = servers I created)
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var s1 = await alice.CreateServerAsync();
        var s1b = await alice.CreateServerAsync();
        var s2 = await bob.CreateServerAsync();

        // When
        var response = await alice.GetAsync(ApiRoutes.Servers.Collection);

        // Then
        var mine = await ApiClient.ReadAsync<ServerDto[]>(response);
        Assert.Equal(new[] { s1.Id, s1b.Id }.Order(), mine.Select(s => s.Id).Order());
        Assert.DoesNotContain(mine, s => s.Id == s2.Id);
        Assert.Equal(new ServerDto(s1.Id, s1.Name, s1.IconUrl, alice.Id), Assert.Single(mine, s => s.Id == s1.Id));
    }

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task Opening_one_of_my_servers_shows_its_channels_in_order_with_everyone_as_the_first_role()
    {
        // Given
        using var alice = await NewUserAsync();
        var created = await alice.CreateServerAsync();

        // When
        var response = await alice.GetAsync(ApiRoutes.Servers.Item(created.Id));

        // Then
        var server = await ApiClient.ReadAsync<ServerDetailDto>(response);
        Assert.Equal(created.Id, server.Id);
        Assert.Equal(created.Name, server.Name);
        Assert.Equal(created.IconUrl, server.IconUrl);
        Assert.Equal(alice.Id, server.OwnerId);
        Assert.Equal(created.Channels, server.Channels);
        Assert.Equal(server.Channels.OrderBy(c => c.Position), server.Channels);
        Assert.Equal(created.Roles, server.Roles);
        Assert.True(server.Roles[0].IsEveryone, "the first role must be @everyone");
        Assert.Equal(AllPermissions, server.MyPermissions);
    }

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task A_server_Im_not_in_looks_exactly_like_one_that_doesnt_exist()
    {
        // Given
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var mine = await alice.CreateServerAsync();
        var bobs = await bob.CreateServerAsync();
        // (control: opening a server does work for its member, so the 404s below aren't just a missing route)
        await alice.GetAsync<ServerDetailDto>(ApiRoutes.Servers.Item(mine.Id));

        // When
        var notMine = await alice.GetAsync(ApiRoutes.Servers.Item(bobs.Id));
        var missing = await alice.GetAsync(ApiRoutes.Servers.Item(Guid.NewGuid()));

        // Then
        var notMineBody = await notMine.AssertProblemAsync(HttpStatusCode.NotFound);
        var missingBody = await missing.AssertProblemAsync(HttpStatusCode.NotFound);
        ProblemAssert.AssertSameProblem(missingBody, notMineBody);
    }
}
