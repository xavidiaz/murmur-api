namespace Infrastructure.Identity;

/// <summary>
/// One refresh token. Only its SHA-256 hash is stored. Tokens rotated from the same sign-in share a
/// <see cref="FamilyId"/>, so reusing a rotated token can revoke the whole session.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }

    /// <summary>Lowercase hex SHA-256 of the token string.</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>The token that replaced this one on refresh (null until rotated).</summary>
    public Guid? ReplacedByTokenId { get; set; }
}
