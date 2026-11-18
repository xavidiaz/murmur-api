using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Api.AcceptanceTests.Infrastructure;

/// <summary>Hand-rolled HS256 JWTs (BCL only) for "token signed with another key" style tests.</summary>
public static class TestJwt
{
    /// <summary>
    /// A well-formed HS256 JWT for <paramref name="userId"/> with the API's issuer/audience, signed with
    /// <paramref name="key"/> (default: a fresh random 64-byte key, i.e. NOT the API's key).
    /// </summary>
    public static string Create(Guid userId, byte[]? key = null, DateTimeOffset? now = null, TimeSpan? lifetime = null,
        string issuer = ApiFactory.JwtIssuer, string audience = ApiFactory.JwtAudience)
    {
        key ??= RandomNumberGenerator.GetBytes(64);
        var iat = now ?? DateTimeOffset.UtcNow;
        var exp = iat + (lifetime ?? TimeSpan.FromMinutes(15));

        var header = new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["sub"] = userId.ToString(),
            ["iss"] = issuer,
            ["aud"] = audience,
            ["iat"] = iat.ToUnixTimeSeconds(),
            ["nbf"] = iat.ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"),
        };

        var signingInput = B64(JsonSerializer.SerializeToUtf8Bytes(header)) + "." + B64(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signature = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(signingInput));
        return signingInput + "." + B64(signature);
    }

    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
