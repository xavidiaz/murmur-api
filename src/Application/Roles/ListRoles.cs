using Application.Common;
using Application.Servers;
using Contracts.Servers;

namespace Application.Roles;

/// <summary>A server's roles in position order, for its members (API-ROLE-02).</summary>
public sealed class ListRoles(IServerRepository servers)
{
    public async Task<Result<IReadOnlyList<RoleDto>>> HandleAsync(Guid userId, Guid serverId, CancellationToken ct)
    {
        var roles = await servers.ListRolesForMemberAsync(serverId, userId, ct);
        if (roles is null)
            return ServerErrors.NotFound;
        return Result<IReadOnlyList<RoleDto>>.Success([.. roles.Select(r => r.ToDto())]);
    }
}
