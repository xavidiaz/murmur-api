using Application.Auth;
using Application.Common;
using Contracts;
using Contracts.Auth;
using Xunit;

namespace Application.Tests.Auth;

/// <summary>Sign-up mistakes are explained — API-AUTH-02: invalid requests are refused before reaching the account store.</summary>
public sealed class SignUpMistakesTests
{
    private readonly FakeUserAccounts _accounts = new();
    private readonly FakeTokenIssuer _tokens = new();
    private SignUp UseCase => new(_accounts, _tokens);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RegisterRequest Valid() => new("alice_x", "alice@example.test", "Passw0rd-123", "Alice");

    public static TheoryData<string, RegisterRequest> BrokenRequests => new()
    {
        { "userName", Valid() with { UserName = "a" } },
        { "userName", Valid() with { UserName = "u" + new string('x', 32) } },
        { "userName", Valid() with { UserName = "u-dash" } },
        { "userName", Valid() with { UserName = "u space" } },
        { "userName", Valid() with { UserName = null! } },
        { "password", Valid() with { Password = "Passw0r" } },
        { "password", Valid() with { Password = "Password-without-digits" } },
        { "password", Valid() with { Password = null! } },
        { "email", Valid() with { Email = "not-an-email" } },
        { "email", Valid() with { Email = "alice.example.test" } },
        { "email", Valid() with { Email = "a@b@c.test" } },
        { "email", Valid() with { Email = "@example.test" } },
        { "email", Valid() with { Email = "alice@" } },
        { "email", Valid() with { Email = "al ice@example.test" } },
        { "email", Valid() with { Email = new string('a', 250) + "@example.test" } },
        { "email", Valid() with { Email = null! } },
        { "displayName", Valid() with { DisplayName = new string('D', 33) } },
    };

    [Theory]
    [Trait("Story", "API-AUTH-02")]
    [MemberData(nameof(BrokenRequests))]
    public async Task A_mistake_is_reported_for_that_field_alone_and_nothing_is_created(string field, RegisterRequest request)
    {
        // When
        var result = await UseCase.HandleAsync(request, Ct);

        // Then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal([field], result.Error.Errors!.Keys);
        Assert.NotEmpty(result.Error.Errors[field]);
        Assert.Null(_accounts.LastCreated);
        Assert.Empty(_tokens.IssuedFor);
    }

    [Fact]
    [Trait("Story", "API-AUTH-02")]
    public async Task Every_broken_field_is_reported_at_once()
    {
        // Given
        var request = new RegisterRequest("a", "nope", "short", new string('D', 33));

        // When
        var result = await UseCase.HandleAsync(request, Ct);

        // Then
        Assert.Equal(["userName", "email", "password", "displayName"], result.Error.Errors!.Keys);
    }

    [Fact]
    [Trait("Story", "API-AUTH-02")]
    public async Task A_mixed_case_username_is_checked_after_lowercasing()
    {
        // When
        var result = await UseCase.HandleAsync(Valid() with { UserName = "Alice_X" }, Ct);

        // Then
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [Trait("Story", "API-AUTH-02")]
    [InlineData(AccountProblem.DuplicateUserName, ErrorCodes.UserNameTaken)]
    [InlineData(AccountProblem.DuplicateEmail, ErrorCodes.EmailTaken)]
    public async Task A_taken_username_or_email_is_a_conflict(AccountProblem problem, string code)
    {
        // Given
        _accounts.NextProblems.Add(problem);

        // When
        var result = await UseCase.HandleAsync(Valid(), Ct);

        // Then
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(code, result.Error.Code);
    }
}
