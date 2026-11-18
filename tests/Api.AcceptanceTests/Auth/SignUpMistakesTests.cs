using System.Text.Json;

namespace Api.AcceptanceTests.Auth;

/// <summary>Sign-up mistakes are explained — API-AUTH-02. As a visitor, I want clear errors so that I can fix my sign-up.</summary>
public sealed class SignUpMistakesTests(PostgresFixture db) : AcceptanceTest(db)
{
    [Fact]
    [Trait("Story", "API-AUTH-02")]
    public async Task A_username_thats_already_taken_is_rejected_even_with_different_capitals()
    {
        // Given
        using var existing = await NewUserAsync();
        using var visitor = Anonymous();
        var request = ValidRequest() with { UserName = existing.User.UserName.ToUpperInvariant() };

        // When
        var response = await visitor.PostAsync(ApiRoutes.Auth.Register, request);

        // Then
        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.UserNameTaken);
    }

    [Fact]
    [Trait("Story", "API-AUTH-02")]
    public async Task An_email_thats_already_in_use_is_rejected_even_with_different_capitals()
    {
        // Given
        var email = Unique.Email();
        using var existing = await ApiClient.RegisterAsync(Api, email: email);
        using var visitor = Anonymous();
        var request = ValidRequest() with { Email = email.ToUpperInvariant() };

        // When
        var response = await visitor.PostAsync(ApiRoutes.Auth.Register, request);

        // Then
        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.EmailTaken);
    }

    public static TheoryData<string, string> BadUserNames => new()
    {
        { "too short (1 char)", "a" },
        { "too long (33 chars)", "u" + Unique.Token(32) },
        { "contains a dash", "u-" + Unique.Token(8) },
        { "contains a space", "u " + Unique.Token(8) },
    };

    [Theory]
    [Trait("Story", "API-AUTH-02")]
    [MemberData(nameof(BadUserNames))]
    public async Task A_badly_formed_username_is_rejected(string why, string userName)
    {
        // Given
        using var visitor = Anonymous();
        var request = ValidRequest() with { UserName = userName };

        // When
        var response = await visitor.PostAsync(ApiRoutes.Auth.Register, request);

        // Then
        var problem = await response.AssertValidationProblemAsync("userName");
        AssertOnlyErrorsFor(problem, why, "userName");
    }

    [Theory]
    [Trait("Story", "API-AUTH-02")]
    [InlineData("shorter than 8 characters", "Passw0r")]
    [InlineData("without a digit", "Password-without-digits")]
    public async Task A_weak_password_is_rejected(string why, string password)
    {
        // Given
        using var visitor = Anonymous();
        var request = ValidRequest() with { Password = password };

        // When
        var response = await visitor.PostAsync(ApiRoutes.Auth.Register, request);

        // Then
        var problem = await response.AssertValidationProblemAsync("password");
        AssertOnlyErrorsFor(problem, why, "password");
    }

    [Theory]
    [Trait("Story", "API-AUTH-02")]
    [InlineData("not-an-email")]
    [InlineData("alice.example.test")]
    public async Task An_invalid_email_is_rejected(string email)
    {
        // Given
        using var visitor = Anonymous();
        var request = ValidRequest() with { Email = email };

        // When
        var response = await visitor.PostAsync(ApiRoutes.Auth.Register, request);

        // Then
        var problem = await response.AssertValidationProblemAsync("email");
        AssertOnlyErrorsFor(problem, email, "email");
    }

    [Fact]
    [Trait("Story", "API-AUTH-02")]
    public async Task A_display_name_longer_than_32_characters_is_rejected()
    {
        // Given
        using var visitor = Anonymous();
        var request = ValidRequest() with { DisplayName = new string('D', 33) };

        // When
        var response = await visitor.PostAsync(ApiRoutes.Auth.Register, request);

        // Then
        var problem = await response.AssertValidationProblemAsync("displayName");
        AssertOnlyErrorsFor(problem, "33-char display name", "displayName");
    }

    /// <summary>A request that would succeed; each test breaks exactly one field.</summary>
    private static RegisterRequest ValidRequest() =>
        new(Unique.UserName(), Unique.Email(), ApiClient.DefaultPassword, "Valid Name");

    /// <summary>The rest of the request is valid, so the 400 must be about this field alone.</summary>
    private static void AssertOnlyErrorsFor(JsonElement problem, string why, string field)
    {
        var keys = problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.True(keys.SequenceEqual([field]), $"{why}: expected errors only for '{field}', got [{string.Join(", ", keys)}]");
    }
}
