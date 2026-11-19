namespace Api.AcceptanceTests.Invites;

/// <summary>Join with an invite — API-INV-04. As a user, I want to join a server through an invite.</summary>
public sealed class JoinWithAnInviteTests(PostgresFixture db) : AcceptanceTest(db)
{
    private const Permissions EveryoneDefaults =
        Permissions.ViewChannel | Permissions.SendMessages | Permissions.CreateInvite;

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Accepting_an_invite_adds_me_to_the_server_and_the_invite_counts_one_use()
    {
        // Given
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await alice.CreateServerAsync(iconUrl: "https://example.test/s.png");
        var invite = await alice.CreateInviteAsync(server.Id);

        // When
        var response = await bob.PostAsync(ApiRoutes.Invites.Accept(invite.Code));

        // Then
        var joined = await ApiClient.ReadAsync<ServerDto>(response);
        Assert.Equal(new ServerDto(server.Id, server.Name, server.IconUrl, alice.Id), joined);

        var bobsServers = await bob.GetAsync<ServerDto[]>(ApiRoutes.Servers.Collection);
        Assert.Contains(bobsServers, s => s.Id == server.Id);

        var opened = await bob.GetAsync<ServerDetailDto>(ApiRoutes.Servers.Item(server.Id));
        Assert.Equal(EveryoneDefaults, opened.MyPermissions);

        var preview = await bob.GetAsync<InviteDto>(ApiRoutes.Invites.Item(invite.Code));
        Assert.Equal(1, preview.Uses);
        Assert.Equal(2, preview.MemberCount);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Accepting_again_when_Im_already_in_changes_nothing()
    {
        // Given
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await alice.CreateServerAsync();
        var invite = await alice.CreateInviteAsync(server.Id);
        var first = await bob.AcceptInviteAsync(invite.Code);

        // When
        var response = await bob.PostAsync(ApiRoutes.Invites.Accept(invite.Code));

        // Then
        var second = await ApiClient.ReadAsync<ServerDto>(response);
        Assert.Equal(first, second);
        var preview = await bob.GetAsync<InviteDto>(ApiRoutes.Invites.Item(invite.Code));
        Assert.Equal(1, preview.Uses);
        Assert.Equal(2, preview.MemberCount);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Accepting_an_invite_to_my_own_server_changes_nothing()
    {
        // Given
        using var alice = await NewUserAsync();
        var server = await alice.CreateServerAsync();
        var invite = await alice.CreateInviteAsync(server.Id);

        // When
        var response = await alice.PostAsync(ApiRoutes.Invites.Accept(invite.Code));

        // Then
        var joined = await ApiClient.ReadAsync<ServerDto>(response);
        Assert.Equal(server.Id, joined.Id);
        var preview = await alice.GetAsync<InviteDto>(ApiRoutes.Invites.Item(invite.Code));
        Assert.Equal(0, preview.Uses);
        Assert.Equal(1, preview.MemberCount);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task An_expired_invite_cant_be_accepted()
    {
        // Given an invite that expires in 60 seconds, on a host whose clock we can move
        await using var api = IsolatedApi();
        using var alice = await NewUserAsync(api: api);
        using var carol = await NewUserAsync(api: api);
        using var bob = await NewUserAsync(api: api);
        var server = await alice.CreateServerAsync();
        var invite = await alice.CreateInviteAsync(server.Id, expiresInSeconds: 60);
        // (control: before the clock moves the invite can be accepted)
        await carol.AcceptInviteAsync(invite.Code);

        // When 61 seconds have passed
        api.Clock.Advance(TimeSpan.FromSeconds(61));
        var response = await bob.PostAsync(ApiRoutes.Invites.Accept(invite.Code));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.Gone, ErrorCodes.InviteExpired);
        var bobsServers = await bob.GetAsync<ServerDto[]>(ApiRoutes.Servers.Collection);
        Assert.DoesNotContain(bobsServers, s => s.Id == server.Id);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task A_used_up_invite_cant_be_accepted()
    {
        // Given a single-use invite that Carol already used
        using var alice = await NewUserAsync();
        using var carol = await NewUserAsync();
        using var dave = await NewUserAsync();
        var server = await alice.CreateServerAsync();
        var invite = await alice.CreateInviteAsync(server.Id, maxUses: 1);
        await carol.AcceptInviteAsync(invite.Code);

        // When
        var response = await dave.PostAsync(ApiRoutes.Invites.Accept(invite.Code));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.Gone, ErrorCodes.InviteExhausted);
        var davesServers = await dave.GetAsync<ServerDto[]>(ApiRoutes.Servers.Collection);
        Assert.DoesNotContain(davesServers, s => s.Id == server.Id);
        // A dead link never shows the server: previewing it now says it's used up.
        var preview = await alice.GetAsync(ApiRoutes.Invites.Item(invite.Code));
        await preview.AssertProblemAsync(HttpStatusCode.Gone, ErrorCodes.InviteExhausted);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Accepting_an_invite_requires_signing_in()
    {
        // Given
        using var alice = await NewUserAsync();
        var server = await alice.CreateServerAsync();
        var invite = await alice.CreateInviteAsync(server.Id);
        using var visitor = Anonymous();

        // When
        var response = await visitor.PostAsync(ApiRoutes.Invites.Accept(invite.Code));

        // Then
        await ApiClient.AssertStatusAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task An_unknown_invite_cant_be_accepted()
    {
        // Given
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await alice.CreateServerAsync();
        var real = await alice.CreateInviteAsync(server.Id);
        // (control: a real code can be accepted, so the 404 below isn't just a missing route)
        await bob.AcceptInviteAsync(real.Code);

        // When
        var response = await bob.PostAsync(ApiRoutes.Invites.Accept(Unique.Token(8)));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }
}
