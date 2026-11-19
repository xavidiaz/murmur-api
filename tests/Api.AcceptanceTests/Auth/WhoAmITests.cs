using System.Security.Cryptography;

namespace Api.AcceptanceTests.Auth;

/// <summary>Who am I — API-AUTH-04. As the app, I want to check my token so that I know whether I'm still signed in.</summary>
public sealed class WhoAmITests(PostgresFixture db) : AcceptanceTest(db)
{
    [Fact]
    [Trait("Story", "API-AUTH-04")]
    public async Task With_a_valid_token_I_get_my_own_profile()
    {
        // Given
        using var me = await NewUserAsync(displayName: "Me Myself");

        // When
        var response = await me.GetAsync(ApiRoutes.Auth.Me);

        // Then
        var profile = await ApiClient.ReadAsync<UserDto>(response);
        Assert.Equal(me.User, profile);
    }

    [Theory]
    [Trait("Story", "API-AUTH-04")]
    [InlineData("without a token")]
    [InlineData("with a garbled token")]
    [InlineData("with a token signed by someone else")]
    public async Task Without_a_token_with_a_garbled_one_or_with_one_signed_by_someone_else_Im_turned_away(string token)
    {
        // Given a real user, and a request carrying one kind of bad credential
        using var me = await NewUserAsync();
        using var app = Anonymous();
        app.UseBearer(token switch
        {
            "without a token" => null,
            "with a garbled token" => "not-a-jwt." + Unique.Token(24),
            // Correct issuer, audience, subject and a valid exp: only the signing key is wrong.
            "with a token signed by someone else" =>
                TestJwt.Create(me.Id, key: RandomNumberGenerator.GetBytes(64), now: Api.Clock.GetUtcNow()),
            _ => throw new ArgumentOutOfRangeException(nameof(token), token, null),
        });

        // When
        var response = await app.GetAsync(ApiRoutes.Auth.Me);

        // Then
        await ApiClient.AssertStatusAsync(response, HttpStatusCode.Unauthorized);
    }
}
