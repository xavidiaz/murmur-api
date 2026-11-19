using Application.Common;
using Application.Servers;
using Domain.Servers;
using DomainPermissions = Domain.Servers.Permissions;

namespace Application.Roles;

/// <summary>Gives a member a role (API-ROLE-03). Needs ManageServer; giving a role they already hold changes nothing.</summary>
public sealed class AssignRole(IServerRepository servers)
{
    public async Task<Result<bool>> HandleAsync(Guid userId, Guid serverId, Guid memberId, Guid roleId, CancellationToken ct)
    {
        var loaded = await ManagedServer.LoadAsync(servers, serverId, userId, DomainPermissions.ManageServer, ct);
        if (!loaded.IsSuccess)
            return loaded.Error;

        // API-ROLE-04 plugs in here: the role hierarchy check (you can only give roles below your highest role), and the
        // concurrency protection for role writes.
        return await RoleAssignments.ApplyAsync(servers, loaded.Value.AssignRole(memberId, roleId), ct);
    }
}

/// <summary>Takes a role away from a member (API-ROLE-03); the same rules as <see cref="AssignRole"/>.</summary>
public sealed class UnassignRole(IServerRepository servers)
{
    public async Task<Result<bool>> HandleAsync(Guid userId, Guid serverId, Guid memberId, Guid roleId, CancellationToken ct)
    {
        var loaded = await ManagedServer.LoadAsync(servers, serverId, userId, DomainPermissions.ManageServer, ct);
        if (!loaded.IsSuccess)
            return loaded.Error;

        // API-ROLE-04 plugs in here too (hierarchy and concurrency), as for AssignRole.
        return await RoleAssignments.ApplyAsync(servers, loaded.Value.UnassignRole(memberId, roleId), ct);
    }
}

internal static class RoleAssignments
{
    /// <summary>
    /// Unknown member or role (another server's role counts as unknown) → 404; <c>@everyone</c> → 400
    /// <c>everyone_role_immutable</c>; no change → success without saving.
    /// </summary>
    public static async Task<Result<bool>> ApplyAsync(IServerRepository servers, RoleAssignmentOutcome outcome, CancellationToken ct)
    {
        switch (outcome)
        {
            case RoleAssignmentOutcome.Done:
                await servers.SaveChangesAsync(ct);
                return true;
            case RoleAssignmentOutcome.NoChange:
                return true;
            case RoleAssignmentOutcome.UnknownMember:
                return ServerErrors.MemberNotFound;
            case RoleAssignmentOutcome.UnknownRole:
                return RoleErrors.NotFound;
            case RoleAssignmentOutcome.EveryoneRole:
                return RoleErrors.EveryoneRoleImmutable;
            default:
                throw new InvalidOperationException($"Unhandled role assignment outcome '{outcome}'.");
        }
    }
}
