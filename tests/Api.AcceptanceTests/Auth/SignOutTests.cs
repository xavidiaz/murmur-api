namespace Api.AcceptanceTests.Auth;

/// <summary>Sign out — API-AUTH-07. As a user, I want to sign out so that this device can't refresh any more.</summary>
public sealed class SignOutTests(PostgresFixture db) : AcceptanceTest(db)
{
    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task After_signing_out_this_devices_refresh_token_no_longer_works()
    {
        // Given
        using var account = await NewUserAsync();
        using var device = await account.LoginAgainAsync();

        // When
        var signOut = await device.PostAsync(ApiRoutes.Auth.Logout, new LogoutRequest(device.RefreshToken));

        // Then
        await ApiClient.AssertStatusAsync(signOut, HttpStatusCode.NoContent);
        var refresh = await RefreshAsync(device.RefreshToken);
        await refresh.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
    }

    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task After_signing_out_this_devices_refresh_token_no_longer_works_even_after_it_was_refreshed()
    {
        // Given: the device has already rotated its refresh token once
        using var account = await NewUserAsync();
        using var device = await account.LoginAgainAsync();
        device.UseAuth(await ApiClient.ReadAsync<AuthResponse>(await RefreshAsync(device.RefreshToken)));

        // When
        var signOut = await device.PostAsync(ApiRoutes.Auth.Logout, new LogoutRequest(device.RefreshToken));

        // Then
        await ApiClient.AssertStatusAsync(signOut, HttpStatusCode.NoContent);
        var refresh = await RefreshAsync(device.RefreshToken);
        await refresh.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
    }

    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task My_other_devices_stay_signed_in()
    {
        // Given
        using var account = await NewUserAsync();
        using var deviceA = await account.LoginAgainAsync();
        using var deviceB = await account.LoginAgainAsync();

        // When
        var signOutA = await deviceA.PostAsync(ApiRoutes.Auth.Logout, new LogoutRequest(deviceA.RefreshToken));
        await ApiClient.AssertStatusAsync(signOutA, HttpStatusCode.NoContent);

        // Then
        var refreshB = await RefreshAsync(deviceB.RefreshToken);
        var b = await ApiClient.ReadAsync<AuthResponse>(refreshB);
        Assert.Equal(account.User, b.User);
    }

    private async Task<HttpResponseMessage> RefreshAsync(string refreshToken)
    {
        using var app = Anonymous();
        return await app.PostAsync(ApiRoutes.Auth.Refresh, new RefreshRequest(refreshToken));
    }
}
