namespace Domain.Invites;

/// <summary>An invite to a server, identified by its code.</summary>
public sealed class Invite
{
    public const int MaxUsesMin = 1;
    public const int MaxUsesMax = 1000;
    public static readonly TimeSpan LifetimeMin = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan LifetimeMax = TimeSpan.FromDays(7);

    private Invite() { } // EF Core

    public string Code { get; private set; } = "";
    public Guid ServerId { get; private set; }
    public Guid CreatorId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When it stops working (exclusive), or null for never.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>How many times it may be used, or null for unlimited.</summary>
    public int? MaxUses { get; private set; }

    public int Uses { get; private set; }

    /// <summary>1–1000, or null for unlimited.</summary>
    public static bool IsValidMaxUses(int? maxUses) => maxUses is null or (>= MaxUsesMin and <= MaxUsesMax);

    /// <summary>From creation to expiry: 60 seconds to 7 days. ("Never expires" is a null expiry, not a lifetime.)</summary>
    public static bool IsValidLifetime(TimeSpan lifetime) => lifetime >= LifetimeMin && lifetime <= LifetimeMax;

    /// <summary>A new, unused invite with a fresh code. <paramref name="now"/> comes from the caller's clock.</summary>
    /// <exception cref="ArgumentException">The use limit or the expiry is outside the allowed range.</exception>
    public static Invite Create(Guid serverId, Guid creatorId, DateTimeOffset now, DateTimeOffset? expiresAt, int? maxUses)
    {
        if (!IsValidMaxUses(maxUses))
            throw new ArgumentException($"An invite's use limit must be {MaxUsesMin}–{MaxUsesMax}, or none.", nameof(maxUses));
        if (expiresAt is { } expiry && !IsValidLifetime(expiry - now))
            throw new ArgumentException($"An invite must expire {LifetimeMin.TotalSeconds:0} s to {LifetimeMax.TotalDays:0} days after creation, or never.", nameof(expiresAt));

        return new Invite
        {
            Code = InviteCode.Generate(),
            ServerId = serverId,
            CreatorId = creatorId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            MaxUses = maxUses,
        };
    }

    /// <summary>
    /// Whether it can be used at <paramref name="now"/>. Expired from the moment <c>now &gt;= ExpiresAt</c>; exhausted
    /// once <c>Uses</c> reaches <c>MaxUses</c>. When both apply, expiry wins.
    /// </summary>
    public InviteUsability CanBeUsed(DateTimeOffset now) =>
        ExpiresAt is { } expiry && now >= expiry ? InviteUsability.Expired
        : MaxUses is { } max && Uses >= max ? InviteUsability.Exhausted
        : InviteUsability.Usable;

    /// <summary>Counts one use if the invite is usable; otherwise changes nothing and says why.</summary>
    public InviteUsability Redeem(DateTimeOffset now)
    {
        var usability = CanBeUsed(now);
        if (usability == InviteUsability.Usable)
            Uses++;
        return usability;
    }
}
