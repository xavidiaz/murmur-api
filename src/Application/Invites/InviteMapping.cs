using Contracts.Invites;
using Domain.Invites;

namespace Application.Invites;

internal static class InviteMapping
{
    public static InviteDto ToDto(this Invite invite, string serverName, string? serverIconUrl, int memberCount) =>
        new(invite.Code, invite.ServerId, serverName, serverIconUrl, invite.CreatorId, invite.ExpiresAt,
            invite.MaxUses, invite.Uses, memberCount);
}
