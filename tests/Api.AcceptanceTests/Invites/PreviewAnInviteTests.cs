namespace Api.AcceptanceTests.Invites;

/// <summary>
/// Preview an invite — API-INV-03. As someone who was sent a link, I want to see which server it's for before joining.
/// </summary>
public sealed class PreviewAnInviteTests(PostgresFixture db) : AcceptanceTest(db)
{
    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task Anyone_can_preview_an_invite_without_signing_in_and_see_the_servers_name_icon_and_member_count()
    {
        // Given
        using var alice = await NewUserAsync();
        var server = await alice.CreateServerAsync(iconUrl: "https://example.test/place.png");
        var created = await alice.CreateInviteAsync(server.Id);
        using var visitor = Anonymous();

        // When
        var response = await visitor.GetAsync(ApiRoutes.Invites.Item(created.Code));

        // Then
        var preview = await ApiClient.ReadAsync<InviteDto>(response);
        Assert.Equal(created.Code, preview.Code);
        Assert.Equal(server.Id, preview.ServerId);
        Assert.Equal(server.Name, preview.ServerName);
        Assert.Equal("https://example.test/place.png", preview.ServerIconUrl);
        Assert.Equal(1, preview.MemberCount);
        AssertSameInvite(created, preview);
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task Someone_signed_in_who_isnt_a_member_can_preview_an_invite_too()
    {
        // Given
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await alice.CreateServerAsync(iconUrl: "https://example.test/place.png");
        var created = await alice.CreateInviteAsync(server.Id);

        // When
        var response = await bob.GetAsync(ApiRoutes.Invites.Item(created.Code));

        // Then
        var preview = await ApiClient.ReadAsync<InviteDto>(response);
        AssertSameInvite(created, preview);
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task An_unknown_code_isnt_found()
    {
        // Given
        using var alice = await NewUserAsync();
        var server = await alice.CreateServerAsync();
        var real = await alice.CreateInviteAsync(server.Id);
        using var visitor = Anonymous();
        // (control: a real code previews, so the 404 below isn't just a missing route)
        await visitor.GetAsync<InviteDto>(ApiRoutes.Invites.Item(real.Code));
        var unknown = Unique.Token(8);

        // When
        var response = await visitor.GetAsync(ApiRoutes.Invites.Item(unknown));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task An_expired_invite_says_so()
    {
        // Given an invite that expires in 60 seconds, on a host whose clock we can move
        await using var api = IsolatedApi();
        using var alice = await NewUserAsync(api: api);
        var server = await alice.CreateServerAsync();
        var invite = await alice.CreateInviteAsync(server.Id, expiresInSeconds: 60);
        using var visitor = Anonymous(api);
        // (control: before the clock moves it previews fine)
        await visitor.GetAsync<InviteDto>(ApiRoutes.Invites.Item(invite.Code));

        // When 61 seconds have passed
        api.Clock.Advance(TimeSpan.FromSeconds(61));
        var response = await visitor.GetAsync(ApiRoutes.Invites.Item(invite.Code));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.Gone, ErrorCodes.InviteExpired);
    }

    /// <summary>
    /// Every field equal; <c>ExpiresAt</c> within 1 ms, since the create response and a later read may differ in
    /// sub-microsecond precision once the value has round-tripped through Postgres.
    /// </summary>
    private static void AssertSameInvite(InviteDto created, InviteDto preview)
    {
        Assert.Equal(created, preview with { ExpiresAt = created.ExpiresAt });
        Assert.Equal(created.ExpiresAt.HasValue, preview.ExpiresAt.HasValue);
        if (created.ExpiresAt is { } expected)
            Assert.InRange(preview.ExpiresAt!.Value, expected.AddMilliseconds(-1), expected.AddMilliseconds(1));
    }
}
