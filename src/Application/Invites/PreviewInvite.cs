using Application.Common;
using Contracts.Invites;
using Domain.Invites;

namespace Application.Invites;

/// <summary>
/// Shows what an invite leads to, to anyone, signed in or not (API-INV-03). A dead link never shows the server:
/// expired → 410 <c>invite_expired</c>, used up → 410 <c>invite_exhausted</c> (expiry wins when both apply).
/// </summary>
public sealed class PreviewInvite(IInviteRepository invites, TimeProvider clock)
{
    public async Task<Result<InviteDto>> HandleAsync(string code, CancellationToken ct)
    {
        // A malformed code can't exist: same answer as an unknown one, without touching the database.
        if (!InviteCode.IsWellFormed(code))
            return InviteErrors.NotFound;

        var preview = await invites.FindPreviewAsync(code, ct);
        if (preview is null)
            return InviteErrors.NotFound;

        return preview.Invite.CanBeUsed(clock.GetUtcNow()) switch
        {
            InviteUsability.Usable => preview.Invite.ToDto(preview.ServerName, preview.ServerIconUrl, preview.MemberCount),
            InviteUsability.Expired => InviteErrors.Expired,
            InviteUsability.Exhausted => InviteErrors.Exhausted,
            var other => throw new InvalidOperationException($"Unhandled invite usability '{other}'."),
        };
    }
}
