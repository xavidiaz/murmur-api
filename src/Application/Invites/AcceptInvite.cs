using Application.Common;
using Contracts.Servers;
using Domain.Invites;
using Domain.Servers;

namespace Application.Invites;

/// <summary>
/// Joins the server an invite leads to (API-INV-04). Already being a member (the owner included) is a no-op that
/// still answers with the server and counts no use. An expired or used-up invite → 410; an unknown one → 404.
/// </summary>
public sealed class AcceptInvite(IInviteRepository invites, TimeProvider clock)
{
    public async Task<Result<ServerDto>> HandleAsync(Guid userId, string code, CancellationToken ct)
    {
        if (!InviteCode.IsWellFormed(code))
            return InviteErrors.NotFound;

        var target = await invites.FindForJoinAsync(code, userId, ct);
        if (target is null)
            return InviteErrors.NotFound;
        if (target.IsMember)
            return target.Server;

        var now = clock.GetUtcNow();
        var outcome = await invites.TryRedeemAsync(code, ServerMember.Join(target.Invite.ServerId, userId, now), now, ct);
        if (outcome is RedeemOutcome.Joined or RedeemOutcome.AlreadyMember)
            return target.Server;

        // Refused by the conditional update. Re-read: I may have joined through a concurrent request of my own
        // (which counts as "already in"); otherwise the domain rule says whether it expired or was used up.
        var reread = await invites.FindForJoinAsync(code, userId, ct);
        if (reread is null)
            return InviteErrors.NotFound;
        if (reread.IsMember)
            return reread.Server;
        return reread.Invite.CanBeUsed(now) switch
        {
            InviteUsability.Expired => InviteErrors.Expired,
            InviteUsability.Exhausted => InviteErrors.Exhausted,
            var other => throw new InvalidOperationException(
                $"The invite was refused by the store but the domain says '{other}' at the same instant."),
        };
    }
}
