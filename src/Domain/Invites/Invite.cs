namespace Domain.Invites;

/// <summary>Whether an invite can be used right now, and if not, why.</summary>
public enum InviteUsability
{
    Usable,
    Expired,
    Exhausted,
}

/// <summary>An invite to a server, identified by its code.</summary>
public sealed class Invite
{
    private Invite() { } // EF Core

    public string Code { get; private set; } = "";
    public Guid ServerId { get; private set; }
    public Guid CreatorId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public int? MaxUses { get; private set; }
    public int Uses { get; private set; }

    public static bool IsValidMaxUses(int? maxUses) => throw new NotImplementedException();
    public static bool IsValidLifetime(TimeSpan lifetime) => throw new NotImplementedException();

    public static Invite Create(Guid serverId, Guid creatorId, DateTimeOffset now, DateTimeOffset? expiresAt, int? maxUses) =>
        throw new NotImplementedException();

    public InviteUsability CanBeUsed(DateTimeOffset now) => throw new NotImplementedException();
    public InviteUsability Redeem(DateTimeOffset now) => throw new NotImplementedException();
}

/// <summary>Generates invite codes.</summary>
public static class InviteCode
{
    public const int Length = 8;
    public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public static string Generate() => throw new NotImplementedException();
}
