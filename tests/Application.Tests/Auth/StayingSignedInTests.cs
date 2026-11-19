using Application.Auth;
using Contracts.Auth;
using Xunit;

namespace Application.Tests.Auth;

/// <summary>Staying signed in — API-AUTH-05: exchanging a refresh token for new tokens.</summary>
public sealed class StayingSignedInTests
{
    private readonly FakeUserAccounts _accounts = new();
    private readonly FakeTokenIssuer _tokens = new();
    private RefreshSession UseCase => new(_accounts, _tokens);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(Guid UserId, string RefreshToken)> SignedInAsync()
    {
        var auth = (await new SignUp(_accounts, _tokens).HandleAsync(
            new RegisterRequest("alice_x", "alice@example.test", "Passw0rd-123", "Alice"), Ct)).Value;
        _tokens.LiveRefreshTokens[auth.RefreshToken] = auth.User.Id;
        return (auth.User.Id, auth.RefreshToken);
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task Refreshing_gives_me_a_new_access_token_and_a_new_refresh_token()
    {
        // Given
        var (userId, refreshToken) = await SignedInAsync();

        // When
        var result = await UseCase.HandleAsync(new RefreshRequest(refreshToken), Ct);

        // Then
        Assert.True(result.IsSuccess);
        var auth = result.Value;
        Assert.Equal(userId, auth.User.Id);
        Assert.Equal("alice_x", auth.User.UserName);
        Assert.NotEqual(refreshToken, auth.RefreshToken);
        Assert.Equal($"access-{userId}-Alice", auth.AccessToken);
        Assert.Equal(FakeTokenIssuer.Now.AddMinutes(15), auth.AccessTokenExpiresAt);
        Assert.Equal(FakeTokenIssuer.Now.AddDays(30), auth.RefreshTokenExpiresAt);
        Assert.Equal([userId], _tokens.AccessTokensFor);
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task I_can_keep_refreshing_with_each_new_refresh_token()
    {
        // Given
        var (_, current) = await SignedInAsync();
        var seen = new HashSet<string> { current };

        // When / Then
        for (var rotation = 1; rotation <= 3; rotation++)
        {
            var result = await UseCase.HandleAsync(new RefreshRequest(current), Ct);
            Assert.True(result.IsSuccess);
            Assert.True(seen.Add(result.Value.RefreshToken));
            current = result.Value.RefreshToken;
        }
    }

    [Theory]
    [Trait("Story", "API-AUTH-05")]
    [InlineData(RefreshTokenProblem.Unknown)]
    [InlineData(RefreshTokenProblem.Expired)]
    [InlineData(RefreshTokenProblem.Revoked)]
    [InlineData(RefreshTokenProblem.AlreadyRotated)]
    public async Task Every_unusable_refresh_token_gets_the_same_answer(RefreshTokenProblem problem)
    {
        // Given
        var (_, refreshToken) = await SignedInAsync();
        _tokens.NextRotationProblem = problem;

        // When
        var result = await UseCase.HandleAsync(new RefreshRequest(refreshToken), Ct);

        // Then
        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Empty(_tokens.AccessTokensFor);
    }

    [Theory]
    [Trait("Story", "API-AUTH-05")]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_refresh_token_is_rejected_without_looking_it_up(string? refreshToken)
    {
        // When
        var result = await UseCase.HandleAsync(new RefreshRequest(refreshToken!), Ct);

        // Then
        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Empty(_tokens.RotationsRequested);
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task A_refresh_token_of_an_account_that_no_longer_exists_is_rejected()
    {
        // Given
        _tokens.LiveRefreshTokens["orphan"] = Guid.NewGuid();

        // When
        var result = await UseCase.HandleAsync(new RefreshRequest("orphan"), Ct);

        // Then
        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Empty(_tokens.AccessTokensFor);
    }
}
