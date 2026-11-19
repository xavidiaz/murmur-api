using Domain.Invites;

namespace Application.Invites;

/// <summary>An invite with what a preview shows about its server; <see cref="MemberCount"/> is counted by the database.</summary>
public sealed record InvitePreview(Invite Invite, string ServerName, string? ServerIconUrl, int MemberCount);

/// <summary>What accepting an invite needs, read in one query: the invite, its server, and whether I'm already in it.</summary>
public sealed record JoinTarget(Invite Invite, Contracts.Servers.ServerDto Server, bool IsMember);

public enum RedeemOutcome
{
    /// <summary>One use was counted and the member added, atomically.</summary>
    Joined,
    /// <summary>The invite was expired or used up (or gone) at that moment; nothing changed.</summary>
    NotUsable,
    /// <summary>The user became a member concurrently; the use was rolled back, nothing changed.</summary>
    AlreadyMember,
}

/// <summary>Port: persistence for invites.</summary>
public interface IInviteRepository
{
    /// <summary>
    /// Saves a new invite. Returns false, saving nothing, when its code is already taken, so the caller can retry
    /// with a fresh code.
    /// </summary>
    Task<bool> TryAddAsync(Invite invite, CancellationToken ct);

    /// <summary>The invite with its server's name, icon and member count, in one query; null when there's no such code.</summary>
    Task<InvitePreview?> FindPreviewAsync(string code, CancellationToken ct);

    /// <summary>The invite, its server and whether <paramref name="userId"/> is already a member; null for an unknown code.</summary>
    Task<JoinTarget?> FindForJoinAsync(string code, Guid userId, CancellationToken ct);

    /// <summary>
    /// In one transaction: counts one use only if the invite is still usable at <paramref name="now"/> (a conditional
    /// update, race-safe), then adds <paramref name="member"/>. If the member already exists, everything is rolled
    /// back, the use included.
    /// </summary>
    Task<RedeemOutcome> TryRedeemAsync(string code, Domain.Servers.ServerMember member, DateTimeOffset now, CancellationToken ct);
}
