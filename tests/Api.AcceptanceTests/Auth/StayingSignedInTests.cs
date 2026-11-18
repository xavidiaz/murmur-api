using System.Security.Cryptography;

namespace Api.AcceptanceTests.Auth;

/// <summary>
/// Staying signed in — API-AUTH-05. As the app, I want to exchange my refresh token for new tokens so that
/// sessions last longer than 15 minutes.
/// </summary>
public sealed class StayingSignedInTests(PostgresFixture db) : AcceptanceTest(db)
{
    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task Refreshing_gives_me_a_new_access_token_and_a_new_refresh_token()
    {
        // Given
        using var me = await NewUserAsync();
        using var app = Anonymous();
        var oldRefreshToken = me.RefreshToken;

        // When
        var response = await app.PostAsync(ApiRoutes.Auth.Refresh, new RefreshRequest(oldRefreshToken));

        // Then
        var auth = await ApiClient.ReadAsync<AuthResponse>(response);
        Assert.Equal(me.User, auth.User);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.NotEqual(oldRefreshToken, auth.RefreshToken);

        app.UseAuth(auth);
        var who = await app.GetAsync<UserDto>(ApiRoutes.Auth.Me);
        Assert.Equal(me.User, who);
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task I_can_keep_refreshing_with_each_new_refresh_token()
    {
        // Given
        using var me = await NewUserAsync();
        using var app = Anonymous();
        var seen = new HashSet<string> { me.RefreshToken };
        var current = me.RefreshToken;

        for (var rotation = 1; rotation <= 3; rotation++)
        {
            // When
            var response = await app.PostAsync(ApiRoutes.Auth.Refresh, new RefreshRequest(current));

            // Then
            var auth = await ApiClient.ReadAsync<AuthResponse>(response);
            Assert.Equal(me.User, auth.User);
            Assert.True(seen.Add(auth.RefreshToken), $"rotation {rotation} returned a refresh token seen before");
            current = auth.RefreshToken;
        }
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task A_made_up_refresh_token_is_rejected()
    {
        // Given
        using var app = Anonymous();
        var madeUp = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // When
        var response = await app.PostAsync(ApiRoutes.Auth.Refresh, new RefreshRequest(madeUp));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
    }
}
