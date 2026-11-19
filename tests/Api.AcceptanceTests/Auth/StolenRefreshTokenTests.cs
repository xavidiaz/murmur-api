namespace Api.AcceptanceTests.Auth;

/// <summary>
/// A stolen refresh token is useless — API-AUTH-06. As a user, I want an already-used refresh token to end that
/// session so that a thief can't keep it alive.
/// </summary>
public sealed class StolenRefreshTokenTests(PostgresFixture db) : AcceptanceTest(db)
{
    [Fact]
    [Trait("Story", "API-AUTH-06")]
    public async Task Reusing_an_old_refresh_token_is_rejected_and_the_whole_session_is_ended()
    {
        // Given
        using var account = await NewUserAsync();
        using var session = await account.LoginAgainAsync();
        var r1 = session.RefreshToken;
        var r2 = (await RefreshOkAsync(r1)).RefreshToken;

        // When
        var reuse = await RefreshAsync(r1);

        // Then
        await reuse.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
        var newest = await RefreshAsync(r2);
        await newest.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
    }

    [Fact]
    [Trait("Story", "API-AUTH-06")]
    public async Task My_other_sessions_are_unaffected()
    {
        // Given
        using var account = await NewUserAsync();
        using var sessionA = await account.LoginAgainAsync();
        using var sessionB = await account.LoginAgainAsync();
        var a1 = sessionA.RefreshToken;
        await RefreshOkAsync(a1);

        // When
        var reuseInA = await RefreshAsync(a1);
        await reuseInA.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);

        // Then
        var refreshB = await RefreshAsync(sessionB.RefreshToken);
        var b = await ApiClient.ReadAsync<AuthResponse>(refreshB);
        Assert.Equal(account.User, b.User);
    }

    [Fact]
    [Trait("Story", "API-AUTH-06")]
    public async Task Losing_a_race_counts_as_reuse()
    {
        // Given
        using var account = await NewUserAsync();
        using var session = await account.LoginAgainAsync();
        var token = session.RefreshToken;
        using var deviceOne = Anonymous();
        using var deviceTwo = Anonymous();
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<HttpResponseMessage> RefreshWhenReleased(ApiClient device)
        {
            await go.Task;
            return await device.PostAsync(ApiRoutes.Auth.Refresh, new RefreshRequest(token));
        }

        // When: the same refresh token is sent twice at once (whatever the interleaving, the second use is reuse)
        var pending = new[] { RefreshWhenReleased(deviceOne), RefreshWhenReleased(deviceTwo) };
        go.SetResult();
        var responses = await Task.WhenAll(pending);

        // Then: at most one winner, every other response is a reuse rejection…
        var winners = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        Assert.True(winners.Count <= 1, $"expected at most one 200, got {winners.Count}");
        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
            await loser.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);

        // …and the whole session is dead.
        if (winners.Count == 1)
        {
            // The losing request alone must have ended the session: the winner's new token is checked first,
            // before anything else touches the family.
            var won = await ApiClient.ReadAsync<AuthResponse>(winners[0]);
            var afterWin = await RefreshAsync(won.RefreshToken);
            await afterWin.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
        }

        var raced = await RefreshAsync(token);
        await raced.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
    }

    private async Task<HttpResponseMessage> RefreshAsync(string refreshToken)
    {
        using var device = Anonymous();
        return await device.PostAsync(ApiRoutes.Auth.Refresh, new RefreshRequest(refreshToken));
    }

    private async Task<AuthResponse> RefreshOkAsync(string refreshToken) =>
        await ApiClient.ReadAsync<AuthResponse>(await RefreshAsync(refreshToken));
}
