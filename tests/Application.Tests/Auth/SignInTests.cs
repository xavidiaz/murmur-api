using Application.Auth;
using Application.Common;
using Contracts;
using Contracts.Auth;
using Xunit;

namespace Application.Tests.Auth;

/// <summary>Sign in — API-AUTH-03: the sign-in use case, with the account store and token issuer faked.</summary>
public sealed class SignInTests
{
    private const string Password = "Passw0rd-123";

    private readonly FakeUserAccounts _accounts = new();
    private readonly FakeTokenIssuer _tokens = new();
    private SignIn UseCase => new(_accounts, _tokens);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<AuthResponse> RegisteredAsync() =>
        (await new SignUp(_accounts, _tokens).HandleAsync(
            new RegisterRequest("alice_x", "alice@example.test", Password, "Alice"), Ct)).Value;

    [Theory]
    [Trait("Story", "API-AUTH-03")]
    [InlineData("alice_x")]
    [InlineData("ALICE_X")]
    [InlineData("alice@example.test")]
    [InlineData("Alice@Example.Test")]
    public async Task Signing_in_with_my_username_or_email_works_whatever_the_capitals(string userNameOrEmail)
    {
        // Given
        var me = (await RegisteredAsync()).User;

        // When
        var result = await UseCase.HandleAsync(new LoginRequest(userNameOrEmail, Password), Ct);

        // Then
        Assert.True(result.IsSuccess);
        Assert.Equal(me, result.Value.User);
        Assert.Equal($"refresh-{me.Id}", result.Value.RefreshToken);
    }

    [Fact]
    [Trait("Story", "API-AUTH-03")]
    public async Task Signing_in_twice_gives_each_sign_in_its_own_refresh_token()
    {
        // Given
        var me = (await RegisteredAsync()).User;
        _tokens.IssuedFor.Clear();

        // When
        await UseCase.HandleAsync(new LoginRequest("alice_x", Password), Ct);
        await UseCase.HandleAsync(new LoginRequest("alice_x", Password), Ct);

        // Then: each sign-in starts its own session
        Assert.Equal([me.Id, me.Id], _tokens.IssuedFor);
    }

    [Fact]
    [Trait("Story", "API-AUTH-03")]
    public async Task A_wrong_password_and_an_unknown_user_get_the_same_answer()
    {
        // Given
        await RegisteredAsync();
        _tokens.IssuedFor.Clear();

        // When
        var wrongPassword = await UseCase.HandleAsync(new LoginRequest("alice_x", "Wrong-passw0rd"), Ct);
        var unknownUser = await UseCase.HandleAsync(new LoginRequest("nobody", "Wrong-passw0rd"), Ct);

        // Then
        Assert.Equal(ErrorKind.Unauthorized, wrongPassword.Error.Kind);
        Assert.Equal(ErrorCodes.InvalidCredentials, wrongPassword.Error.Code);
        Assert.Equal(wrongPassword.Error, unknownUser.Error);
        Assert.Empty(_tokens.IssuedFor);
    }

    [Theory]
    [Trait("Story", "API-AUTH-03")]
    [InlineData(null, Password)]
    [InlineData("", Password)]
    [InlineData("alice_x", null)]
    [InlineData("alice_x", "")]
    public async Task Missing_credentials_get_the_same_answer_too(string? userNameOrEmail, string? password)
    {
        // Given
        await RegisteredAsync();

        // When
        var result = await UseCase.HandleAsync(new LoginRequest(userNameOrEmail!, password!), Ct);

        // Then
        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }
}
