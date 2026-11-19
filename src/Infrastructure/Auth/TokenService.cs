using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Auth;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    /// Rotates within the token's family, or, if the token was already rotated, ends the whole family
    /// (API-AUTH-06, strict: a replay and the loser of a race are both reuse).
    /// </summary>
    /// <remarks>
    /// <para>Every write to a family (rotation or revocation) runs in a transaction that first takes a per-family
    /// advisory lock (<c>pg_advisory_xact_lock</c>), and the token is re-read under that lock. So writes to one family
    /// are serialized and each one sees every earlier write already committed.</para>
    /// <para>Why the lock is needed: the family revocation is a single <c>UPDATE … WHERE FamilyId = x</c>, and under
    /// READ COMMITTED it only sees rows that were committed when the statement started. Without the lock, a reuse could
    /// race a legitimate rotation of the newest token (R2 → R3). The revocation would wait on R2's row lock, find R2
    /// already retired, and never see R3, so R3 would survive and the thief's session would live on. With the lock,
    /// the reuse either runs before the rotation, which then finds R2 revoked and refuses, or after it has committed,
    /// in which case the revocation sees R3.</para>
    /// <para>In a race between two refreshes of the same token, the loser waits for the lock until the winner has
    /// committed both the retirement and its new token. It then sees <c>ReplacedByTokenId</c> set and revokes the
    /// family, including the winner's new token.</para>
    /// <para>The conditional <c>UPDATE … WHERE Id = x AND RevokedAt IS NULL AND ReplacedByTokenId IS NULL</c>, which must
    /// change exactly one row, stays as a second line of defence. If it ever changes zero rows, that is also treated
    /// as reuse.</para>
    /// </remarks>
    public async Task<RotationResult> RotateRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var familyId = await db.RefreshTokens.Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId).SingleOrDefaultAsync(ct);
        if (familyId is not { } family)
            return RotationResult.Refused(RefreshTokenProblem.Unknown);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockFamilyAsync(family, ct);

        // Re-read under the lock: every earlier rotation/revocation of this family has committed.
        var current = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == hash, ct);
        if (current.ReplacedByTokenId is not null)
            return await EndFamilyAsync(transaction, family, ct);
        if (current.RevokedAt is not null)
            return RotationResult.Refused(RefreshTokenProblem.Revoked);

        var now = clock.GetUtcNow();
        if (current.ExpiresAt <= now)
            return RotationResult.Refused(RefreshTokenProblem.Expired);

        var (next, nextEntity) = AddRefreshToken(current.UserId, family);
        var retired = await db.RefreshTokens
            .Where(t => t.Id == current.Id && t.RevokedAt == null && t.ReplacedByTokenId == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByTokenId, nextEntity.Id), ct);
        if (retired != 1)
        {
            // Unreachable while every writer takes the family lock; if it happens anyway, treat it as reuse.
            db.RefreshTokens.Entry(nextEntity).State = EntityState.Detached;
            return await EndFamilyAsync(transaction, family, ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return RotationResult.Rotated(new RotatedRefreshToken(current.UserId, next.Token, next.ExpiresAt));
    }

    /// <summary>Reuse detected: revoke every live token of the family (under the family lock) and commit.</summary>
    private async Task<RotationResult> EndFamilyAsync(IDbContextTransaction transaction, Guid familyId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        await transaction.CommitAsync(ct);
        return RotationResult.Refused(RefreshTokenProblem.AlreadyRotated);
    }

    /// <summary>
    /// Serializes writes to one token family until the current transaction ends. Every write to a family must take it.
    /// Two families whose keys collide just wait for each other briefly.
    /// </summary>
    private Task LockFamilyAsync(Guid familyId, CancellationToken ct)
    {
        Span<byte> bytes = stackalloc byte[16];
        familyId.TryWriteBytes(bytes);
        var key = BitConverter.ToInt64(bytes[..8]) ^ BitConverter.ToInt64(bytes[8..]);
        return db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct);
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
