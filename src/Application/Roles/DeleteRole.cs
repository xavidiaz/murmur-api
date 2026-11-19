using Application.Common;
using Application.Servers;
using Domain.Servers;
using DomainPermissions = Domain.Servers.Permissions;

namespace Application.Roles;

/// <summary>
/// Deletes a role (API-ROLE-02): its holders lose it and the roles above it close the gap. @everyone can't be deleted.
/// Needs ManageServer.
/// </summary>
public sealed class DeleteRole(IServerRepository servers)
{
    public async Task<Result<bool>> HandleAsync(Guid userId, Guid serverId, Guid roleId, CancellationToken ct)
    {
        var loaded = await ManagedServer.LoadAsync(servers, serverId, userId, DomainPermissions.ManageServer, ct);
        if (!loaded.IsSuccess)
            return loaded.Error;

        switch (loaded.Value.DeleteRole(roleId))
        {
            case RoleChangeOutcome.UnknownRole:
                return RoleErrors.NotFound;
            case RoleChangeOutcome.EveryoneRoleImmutable:
                return RoleErrors.EveryoneRoleImmutable;
        }

        await servers.SaveChangesAsync(ct);
        return true;
    }
}
