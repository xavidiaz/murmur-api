namespace Api.AcceptanceTests.Auth;

/// <summary>Sign up — API-AUTH-01. As a visitor, I want to create an account so that I can start chatting.</summary>
public sealed class SignUpTests(PostgresFixture db) : AcceptanceTest(db)
{
    [Fact]
    [Trait("Story", "API-AUTH-01")]
    public async Task Signing_up_with_valid_details_logs_me_in_straight_away()
    {
        // Given
        using var visitor = Anonymous();
        var userName = Unique.UserName();
        var request = new RegisterRequest(userName, Unique.Email(), ApiClient.DefaultPassword, "Alice Example");
        var before = Api.Clock.GetUtcNow();

        // When
        var response = await visitor.PostAsync(ApiRoutes.Auth.Register, request);

        // Then
        var auth = await ApiClient.ReadAsync<AuthResponse>(response, HttpStatusCode.Created);
        Assert.NotEqual(Guid.Empty, auth.User.Id);
        Assert.Equal(userName, auth.User.UserName);
        Assert.Equal("Alice Example", auth.User.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.True(auth.AccessTokenExpiresAt > before, "access token should expire in the future");
        Assert.True(auth.RefreshTokenExpiresAt > auth.AccessTokenExpiresAt, "refresh token should outlive the access token");
    }

    [Fact]
    [Trait("Story", "API-AUTH-01")]
    public async Task The_access_token_from_signing_up_works_when_I_ask_who_am_I()
    {
        // Given
        using var me = await NewUserAsync(displayName: "Who Am I");

        // When
        var response = await me.GetAsync(ApiRoutes.Auth.Me);

        // Then
        var user = await ApiClient.ReadAsync<UserDto>(response);
        Assert.Equal(me.User, user);
    }

    [Fact]
    [Trait("Story", "API-AUTH-01")]
    public async Task My_username_is_stored_in_lowercase()
    {
        // Given
        using var visitor = Anonymous();
        var lower = Unique.UserName("alice_x");
        var mixed = string.Concat(lower.Select((c, i) => i % 2 == 0 ? char.ToUpperInvariant(c) : c)); // e.g. AlIcE_X_…
        Assert.NotEqual(lower, mixed);

        // When
        var auth = await visitor.PostAsync<AuthResponse>(ApiRoutes.Auth.Register,
            new RegisterRequest(mixed, Unique.Email(), ApiClient.DefaultPassword, "Alice"), HttpStatusCode.Created);

        // Then
        Assert.Equal(lower, auth.User.UserName);
        visitor.UseAuth(auth);
        var me = await visitor.GetAsync<UserDto>(ApiRoutes.Auth.Me);
        Assert.Equal(lower, me.UserName);
    }

    [Theory]
    [Trait("Story", "API-AUTH-01")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task If_I_dont_pick_a_display_name_my_username_is_used(string? displayName)
    {
        // Given
        using var visitor = Anonymous();
        var userName = Unique.UserName();

        // When
        var auth = await visitor.PostAsync<AuthResponse>(ApiRoutes.Auth.Register,
            new RegisterRequest(userName, Unique.Email(), ApiClient.DefaultPassword, displayName), HttpStatusCode.Created);

        // Then
        Assert.Equal(userName, auth.User.DisplayName);
    }
}
