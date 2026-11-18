using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.AcceptanceTests.Auth;

/// <summary>Sign in — API-AUTH-03. As a user, I want to sign in with my username or my email so that I get fresh tokens.</summary>
public sealed class SignInTests(PostgresFixture db) : AcceptanceTest(db)
{
    /// <summary>Problem fields that legitimately differ per request and so are left out of the "identical bodies" comparison.</summary>
    private static readonly string[] PerRequestFields = ["traceId", "instance"];

    [Theory]
    [Trait("Story", "API-AUTH-03")]
    [InlineData("as registered")]
    [InlineData("upper case")]
    [InlineData("mixed case")]
    public async Task Signing_in_with_my_username_works_whatever_the_capitals(string capitals)
    {
        // Given
        using var me = await NewUserAsync();
        using var device = Anonymous();
        var userName = Recase(me.User.UserName, capitals);

        // When
        var response = await device.PostAsync(ApiRoutes.Auth.Login, new LoginRequest(userName, me.Password!));

        // Then
        await AssertSignedInAsAsync(device, response, me.User);
    }

    [Theory]
    [Trait("Story", "API-AUTH-03")]
    [InlineData("as registered")]
    [InlineData("upper case")]
    [InlineData("mixed case")]
    public async Task Signing_in_with_my_email_works_too(string capitals)
    {
        // Given
        var email = Unique.Email();
        using var me = await ApiClient.RegisterAsync(Api, email: email);
        using var device = Anonymous();

        // When
        var response = await device.PostAsync(ApiRoutes.Auth.Login, new LoginRequest(Recase(email, capitals), me.Password!));

        // Then
        await AssertSignedInAsAsync(device, response, me.User);
    }

    [Fact]
    [Trait("Story", "API-AUTH-03")]
    public async Task Signing_in_twice_gives_each_sign_in_its_own_refresh_token()
    {
        // Given
        using var me = await NewUserAsync();
        using var device = Anonymous();
        var login = new LoginRequest(me.User.UserName, me.Password!);

        // When
        var first = await device.PostAsync<AuthResponse>(ApiRoutes.Auth.Login, login);
        var second = await device.PostAsync<AuthResponse>(ApiRoutes.Auth.Login, login);

        // Then
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(me.RefreshToken, first.RefreshToken);
    }

    [Fact]
    [Trait("Story", "API-AUTH-03")]
    public async Task A_wrong_password_and_an_unknown_user_get_the_same_answer()
    {
        // Given
        using var existing = await NewUserAsync();
        using var device = Anonymous();

        // When
        var wrongPassword = await device.PostAsync(ApiRoutes.Auth.Login,
            new LoginRequest(existing.User.UserName, "Wrong-passw0rd"));
        var unknownUser = await device.PostAsync(ApiRoutes.Auth.Login,
            new LoginRequest(Unique.UserName("nobody"), "Wrong-passw0rd"));

        // Then
        var wrongPasswordBody = await wrongPassword.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
        var unknownUserBody = await unknownUser.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
        var a = WithoutPerRequestFields(wrongPasswordBody);
        var b = WithoutPerRequestFields(unknownUserBody);
        Assert.True(JsonNode.DeepEquals(a, b),
            $"bodies differ (ignoring {string.Join(", ", PerRequestFields)}):\n wrong password: {a}\n unknown user:   {b}");
    }

    private static async Task AssertSignedInAsAsync(ApiClient device, HttpResponseMessage response, UserDto expected)
    {
        var auth = await ApiClient.ReadAsync<AuthResponse>(response);
        Assert.Equal(expected, auth.User);
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));

        device.UseAuth(auth);
        var me = await device.GetAsync<UserDto>(ApiRoutes.Auth.Me);
        Assert.Equal(expected, me);
    }

    private static string Recase(string value, string capitals) => capitals switch
    {
        "as registered" => value,
        "upper case" => value.ToUpperInvariant(),
        "mixed case" => string.Concat(value.Select((c, i) => i % 2 == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c))),
        _ => throw new ArgumentOutOfRangeException(nameof(capitals), capitals, null),
    };

    private static JsonObject WithoutPerRequestFields(JsonElement problem)
    {
        var node = JsonNode.Parse(problem.GetRawText())!.AsObject();
        foreach (var field in PerRequestFields)
            node.Remove(field);
        return node;
    }
}
