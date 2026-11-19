using Application.Common;
using Contracts.Servers;

namespace Application.Servers;

/// <summary>
/// Everyone in a server, for its members: profile, nickname, join time and roles (<c>@everyone</c> isn't listed), in
/// join order (then user id). Pulled forward from API-SRV-06 for API-ROLE-03.
/// </summary>
public sealed class ListMembers(IServerRepository servers)
{
    public async Task<Result<IReadOnlyList<MemberDto>>> HandleAsync(Guid userId, Guid serverId, CancellationToken ct)
    {
        var members = await servers.ListMembersForMemberAsync(serverId, userId, ct);
        if (members is null)
            return ServerErrors.NotFound;
        return Result<IReadOnlyList<MemberDto>>.Success([.. members.Select(m => new MemberDto(m.User, m.Nickname, m.JoinedAt, m.RoleIds))]);
    }
}
