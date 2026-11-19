namespace Api.AcceptanceTests.Invites;

/// <summary>Create an invite — API-INV-02. As a member, I want an invite link to share with friends.</summary>
public sealed class CreateAnInviteTests(PostgresFixture db) : AcceptanceTest(db)
{
    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task By_default_an_invite_lasts_24_hours_and_can_be_used_any_number_of_times()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync(iconUrl: "https://example.test/icon.png");
        var before = Api.Clock.GetUtcNow();

        // When
        var response = await owner.PostAsync(ApiRoutes.Servers.Invites(server.Id), new { });

        // Then
        var invite = await ApiClient.ReadAsync<InviteDto>(response, HttpStatusCode.Created);
        Assert.Matches("^[0-9A-Za-z]{8}$", invite.Code);
        Assert.Equal(server.Id, invite.ServerId);
        Assert.Equal(server.Name, invite.ServerName);
        Assert.Equal(server.IconUrl, invite.ServerIconUrl);
        Assert.Equal(owner.Id, invite.CreatorId);
        Assert.NotNull(invite.ExpiresAt);
        // The fake clock auto-advances 1 ms per read (and other tests read it too), so allow a little slack.
        Assert.InRange(invite.ExpiresAt.Value, before.AddHours(24).AddSeconds(-1), before.AddHours(24).AddMinutes(1));
        Assert.Null(invite.MaxUses);
        Assert.Equal(0, invite.Uses);
        Assert.Equal(1, invite.MemberCount);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task I_can_make_an_invite_that_never_expires()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();

        // When
        var response = await owner.PostAsync(ApiRoutes.Servers.Invites(server.Id), new CreateInviteRequest(null, 0));

        // Then
        var invite = await ApiClient.ReadAsync<InviteDto>(response, HttpStatusCode.Created);
        Assert.Null(invite.ExpiresAt);
    }

    [Theory]
    [Trait("Story", "API-INV-02")]
    [InlineData("expiresInSeconds", null, 30)]
    [InlineData("expiresInSeconds", null, 604801)]
    [InlineData("maxUses", 0, null)]
    [InlineData("maxUses", 1001, null)]
    public async Task Silly_settings_are_rejected(string field, int? maxUses, int? expiresInSeconds)
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();

        // When
        var response = await owner.PostAsync(ApiRoutes.Servers.Invites(server.Id), new CreateInviteRequest(maxUses, expiresInSeconds));

        // Then
        var problem = await response.AssertValidationProblemAsync(field);
        var keys = problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.True(keys.SequenceEqual([field]), $"expected errors only for '{field}', got [{string.Join(", ", keys)}]");
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task Each_invite_gets_its_own_code()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();

        // When
        var first = await owner.CreateInviteAsync(server.Id);
        var second = await owner.CreateInviteAsync(server.Id);

        // Then
        Assert.NotEqual(first.Code, second.Code);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task Creating_an_invite_requires_signing_in()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        using var visitor = Anonymous();

        // When
        var response = await visitor.PostAsync(ApiRoutes.Servers.Invites(server.Id), new { });

        // Then
        await ApiClient.AssertStatusAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task Someone_outside_the_server_cant_create_an_invite_for_it()
    {
        // Given
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var alicesServer = await alice.CreateServerAsync();
        await bob.CreateServerAsync();
        // (control: a member can create an invite there, so the 404 below isn't just a missing route)
        await alice.CreateInviteAsync(alicesServer.Id);

        // When
        var response = await bob.PostAsync(ApiRoutes.Servers.Invites(alicesServer.Id), new { });

        // Then
        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }
}
