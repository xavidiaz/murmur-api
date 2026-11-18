using Application.Auth;
using Application.Common;
using Contracts;
using Contracts.Auth;
using Xunit;

namespace Application.Tests.Auth;

/// <summary>Sign up — API-AUTH-01: the sign-up use case, with the account store and token issuer faked.</summary>
public sealed class SignUpTests
{
    private readonly FakeUserAccounts _accounts = new();
    private readonly FakeTokenIssuer _tokens = new();
    private SignUp UseCase => new(_accounts, _tokens);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Story", "API-AUTH-01")]
    public async Task Signing_up_with_valid_details_logs_me_in_straight_away()
    {
        // Given
        var request = new RegisterRequest("alice_x", "alice@example.test", "Passw0rd-123", "Alice Example");

        // When
        var result = await UseCase.HandleAsync(request, Ct);

        // Then
        Assert.True(result.IsSuccess);
        var auth = result.Value;
        Assert.Equal("alice_x", auth.User.UserName);
        Assert.Equal("Alice Example", auth.User.DisplayName);
        Assert.Null(auth.User.AvatarUrl);
        Assert.Equal([auth.User.Id], _tokens.IssuedFor);
        Assert.Equal($"access-{auth.User.Id}", auth.AccessToken);
        Assert.Equal($"refresh-{auth.User.Id}", auth.RefreshToken);
        Assert.Equal(FakeTokenIssuer.Now.AddMinutes(15), auth.AccessTokenExpiresAt);
        Assert.Equal(FakeTokenIssuer.Now.AddDays(30), auth.RefreshTokenExpiresAt);
        Assert.Equal("alice@example.test", _accounts.LastCreated!.Email);
        Assert.Equal("Passw0rd-123", _accounts.LastPassword);
    }

    [Fact]
    [Trait("Story", "API-AUTH-01")]
    public async Task My_username_is_stored_in_lowercase()
    {
        // Given
        var request = new RegisterRequest("Alice_X", "alice@example.test", "Passw0rd-123", "Alice");

        // When
        var result = await UseCase.HandleAsync(request, Ct);

        // Then
        Assert.Equal("alice_x", _accounts.LastCreated!.UserName);
        Assert.Equal("alice_x", result.Value.User.UserName);
    }

    [Theory]
    [Trait("Story", "API-AUTH-01")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task If_I_dont_pick_a_display_name_my_username_is_used(string? displayName)
    {
        // Given
        var request = new RegisterRequest("Alice_X", "alice@example.test", "Passw0rd-123", displayName);

        // When
        var result = await UseCase.HandleAsync(request, Ct);

        // Then
        Assert.Equal("alice_x", _accounts.LastCreated!.DisplayName);
        Assert.Equal("alice_x", result.Value.User.DisplayName);
    }

    [Fact]
    [Trait("Story", "API-AUTH-01")]
    public async Task No_tokens_are_issued_when_the_account_cannot_be_created()
    {
        // Given
        _accounts.NextProblems.Add(AccountProblem.DuplicateUserName);
        var request = new RegisterRequest("alice_x", "alice@example.test", "Passw0rd-123", null);

        // When
        var result = await UseCase.HandleAsync(request, Ct);

        // Then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(ErrorCodes.UserNameTaken, result.Error.Code);
        Assert.Empty(_tokens.IssuedFor);
    }

    [Fact]
    [Trait("Story", "API-AUTH-01")]
    public async Task Who_am_I_returns_the_account_I_signed_up_with()
    {
        // Given
        var signedUp = (await UseCase.HandleAsync(
            new RegisterRequest("alice_x", "alice@example.test", "Passw0rd-123", "Alice"), Ct)).Value;

        // When
        var me = await new GetCurrentUser(_accounts).HandleAsync(signedUp.User.Id, Ct);

        // Then
        Assert.Equal(signedUp.User, me);
    }
}
