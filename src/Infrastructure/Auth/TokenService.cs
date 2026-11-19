using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Auth;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        var access = CreateAccessToken(user);
        var (refresh, _) = AddRefreshToken(user.Id, familyId: Guid.NewGuid());
        await db.SaveChangesAsync(ct);
        return new IssuedTokens(access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt);
    }

    /// <summary>
    /// Rotates within the token's family. Race-safe: the old token is retired with a conditional
    /// <c>UPDATE … WHERE Id = x AND RevokedAt IS NULL AND ReplacedByTokenId IS NULL AND ExpiresAt &gt; now</c>
    /// inside a transaction, and only the caller whose update changed exactly one row inserts the replacement.
    /// Postgres row locking makes concurrent updaters wait for the winner and then match zero rows.
    /// </summary>
    public async Task<RotationResult> RotateRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var current = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (current is null)
            return RotationResult.Refused(RefreshTokenProblem.Unknown);
        if (current.ReplacedByTokenId is not null)
            return RotationResult.Refused(RefreshTokenProblem.AlreadyRotated);
        if (current.RevokedAt is not null)
            return RotationResult.Refused(RefreshTokenProblem.Revoked);

        var now = clock.GetUtcNow();
        if (current.ExpiresAt <= now)
            return RotationResult.Refused(RefreshTokenProblem.Expired);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (next, nextEntity) = AddRefreshToken(current.UserId, current.FamilyId);

        var retired = await db.RefreshTokens
            .Where(t => t.Id == current.Id && t.RevokedAt == null && t.ReplacedByTokenId == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByTokenId, nextEntity.Id), ct);
        if (retired != 1)
        {
            // Someone else rotated (or revoked) it between our read and our update.
            db.RefreshTokens.Entry(nextEntity).State = EntityState.Detached;
            return RotationResult.Refused(RefreshTokenProblem.AlreadyRotated);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return RotationResult.Rotated(new RotatedRefreshToken(current.UserId, next.Token, next.ExpiresAt));
    }

    /// <summary>SHA-256 (lowercase hex) of a refresh token, as stored in <see cref="RefreshToken.TokenHash"/>.</summary>
    public static string Hash(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    public AccessToken CreateAccessToken(UserAccount user)
    {
        var jwt = options.Value;
        // JWT times have one-second resolution; truncate so the reported expiry equals the token's "exp".
        var now = Truncate(clock.GetUtcNow(), TimeSpan.TicksPerSecond);
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
        return new AccessToken(token, expiresAt);
    }

    private ((string Token, DateTimeOffset ExpiresAt) Issued, RefreshToken Entity) AddRefreshToken(Guid userId, Guid familyId)
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        // Postgres stores microseconds; truncate so the expiry we report equals the stored one.
        var now = Truncate(clock.GetUtcNow(), TimeSpan.TicksPerMicrosecond);
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
        return ((token, entity.ExpiresAt), entity);
    }

    private static DateTimeOffset Truncate(DateTimeOffset value, long ticks) =>
        new(value.Ticks - value.Ticks % ticks, value.Offset);
}
