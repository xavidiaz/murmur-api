using Application.Auth;
using Contracts.Auth;
using Xunit;

namespace Application.Tests.Auth;

/// <summary>Sign out — API-AUTH-07: the sign-out use case, with the token issuer faked.</summary>
public sealed class SignOutTests
{
    private readonly FakeTokenIssuer _tokens = new();
    private SignOut UseCase => new(_tokens);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task Signing_out_ends_the_session_of_the_given_refresh_token_for_the_signed_in_user()
    {
        // Given
        var me = Guid.NewGuid();

        // When
        await UseCase.HandleAsync(me, new LogoutRequest("refresh-1"), Ct);

        // Then
        Assert.Equal([(me, "refresh-1")], _tokens.SessionsEnded);
    }

    [Theory]
    [Trait("Story", "API-AUTH-07")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Signing_out_without_a_refresh_token_does_nothing(string? refreshToken)
    {
        // When
        await UseCase.HandleAsync(Guid.NewGuid(), new LogoutRequest(refreshToken!), Ct);

        // Then
        Assert.Empty(_tokens.SessionsEnded);
    }
}
