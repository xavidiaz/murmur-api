using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Auth;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

/// <summary>
/// Issues HS256 access tokens (15 min) and opaque refresh tokens (30 days, stored as SHA-256 hashes).
/// Every timestamp comes from a fresh <see cref="TimeProvider.GetUtcNow"/> call on the DI clock.
/// </summary>
public sealed class TokenService(AppDbContext db, TimeProvider clock, IOptions<JwtOptions> options) : ITokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    public async Task<IssuedTokens> IssueAsync(UserAccount user, CancellationToken ct)
    {
        var (accessToken, accessExpiresAt) = CreateAccessToken(user);
        var (refreshToken, refreshExpiresAt) = AddRefreshToken(user.Id, familyId: Guid.NewGuid());
        await db.SaveChangesAsync(ct);
        return new IssuedTokens(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt);
    }

    /// <summary>SHA-256 (lowercase hex) of a refresh token, as stored in <see cref="RefreshToken.TokenHash"/>.</summary>
    public static string Hash(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(UserAccount user)
    {
        var jwt = options.Value;
        // JWT times have one-second resolution; truncate so the reported expiry equals the token's "exp".
        var now = TruncateToSeconds(clock.GetUtcNow());
        var expiresAt = now + JwtOptions.AccessTokenLifetime;

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });
        return (token, expiresAt);
    }

    private (string Token, DateTimeOffset ExpiresAt) AddRefreshToken(Guid userId, Guid familyId)
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();
        var entity = new RefreshToken
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now + JwtOptions.RefreshTokenLifetime,
        };
        db.RefreshTokens.Add(entity);
        return (token, entity.ExpiresAt);
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Offset);
}
