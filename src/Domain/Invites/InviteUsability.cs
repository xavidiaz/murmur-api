namespace Domain.Invites;

/// <summary>Whether an invite can be used right now, and if not, why (Application maps these to 410 codes).</summary>
public enum InviteUsability
{
    Usable,
    /// <summary>Its expiry time has been reached.</summary>
    Expired,
    /// <summary>It has been used as many times as it allows.</summary>
    Exhausted,
}
