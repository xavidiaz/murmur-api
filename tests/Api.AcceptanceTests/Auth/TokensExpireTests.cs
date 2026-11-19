namespace Api.AcceptanceTests.Auth;

/// <summary>
/// Tokens expire — API-AUTH-08. As an operator, I want short-lived access tokens so that a leaked token stops working quickly.
/// These tests move the clock, so each uses its own isolated API host.
/// </summary>
public sealed class TokensExpireTests(PostgresFixture db) : AcceptanceTest(db)
{
    [Fact]
    [Trait("Story", "API-AUTH-08")]
    public async Task An_access_token_works_for_15_minutes_and_not_after()
    {
        // Given
        await using var api = IsolatedApi();
        using var me = await NewUserAsync(api: api);

        // When 14 minutes have passed
        api.Clock.Advance(TimeSpan.FromMinutes(14));

        // Then it still works
        var at14 = await me.GetAsync(ApiRoutes.Auth.Me);
        await ApiClient.AssertStatusAsync(at14, HttpStatusCode.OK);

        // When 16 minutes have passed
        api.Clock.Advance(TimeSpan.FromMinutes(2));

        // Then it is rejected
        var at16 = await me.GetAsync(ApiRoutes.Auth.Me);
        await ApiClient.AssertStatusAsync(at16, HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "API-AUTH-08")]
    public async Task A_refresh_token_stops_working_after_30_days()
    {
        // Given two separate sign-ins issued at the same moment
        await using var api = IsolatedApi();
        using var account = await NewUserAsync(api: api);
        using var control = await account.LoginAgainAsync();
        using var device = await account.LoginAgainAsync();
        using var app = Anonymous(api);

        // And (control) after 29 days a refresh token from that moment still works
        api.Clock.Advance(TimeSpan.FromDays(29));
        var before = await app.PostAsync(ApiRoutes.Auth.Refresh, new RefreshRequest(control.RefreshToken));
        await ApiClient.AssertStatusAsync(before, HttpStatusCode.OK);

        // When 31 days have passed
        api.Clock.Advance(TimeSpan.FromDays(2));
        var after = await app.PostAsync(ApiRoutes.Auth.Refresh, new RefreshRequest(device.RefreshToken));

        // Then
        await after.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
    }
}
