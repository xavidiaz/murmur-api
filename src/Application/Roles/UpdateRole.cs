using Application.Common;
using Application.Servers;
using Contracts.Servers;
using Domain.Servers;
using DomainPermissions = Domain.Servers.Permissions;

namespace Application.Roles;

/// <summary>
/// Changes a role (API-ROLE-02); null fields stay as they are. <c>Position</c> moves the role to that place (1..number
/// of roles besides @everyone) and renumbers the others. @everyone can't be renamed or moved (400
/// <c>everyone_role_immutable</c>), but its colour and permissions can change. Needs ManageServer.
/// </summary>
public sealed class UpdateRole(IServerRepository servers)
{
    public async Task<Result<RoleDto>> HandleAsync(Guid userId, Guid serverId, Guid roleId, UpdateRoleRequest request, CancellationToken ct)
    {
        var loaded = await ManagedServer.LoadAsync(servers, serverId, userId, DomainPermissions.ManageServer, ct);
        if (!loaded.IsSuccess)
            return loaded.Error;
        var server = loaded.Value;

        var role = server.Roles.FirstOrDefault(r => r.Id == roleId);
        if (role is null)
            return RoleErrors.NotFound;
        if (role.IsEveryone && (request.Name is not null || request.Position is not null))
            return RoleErrors.EveryoneRoleImmutable;

        var permissions = request.Permissions is { } p ? (DomainPermissions)(long)p : (DomainPermissions?)null;
        var errors = new Dictionary<string, string[]>();
        if (request.Name is not null)
            RoleValidation.Name(errors, request.Name);
        RoleValidation.Color(errors, request.Color);
        RoleValidation.Permissions(errors, permissions);
        if (request.Position is { } position && !server.IsValidRolePosition(position))
            errors["position"] = [$"Position must be from 1 to {server.Roles.Count - 1}."];
        if (errors.Count > 0)
            return AppError.Validation(errors);

        // Everything is valid, so the domain can only report outcomes already ruled out above.
        Expect(server.UpdateRole(roleId, request.Name, request.Color, permissions));
        if (request.Position is { } newPosition)
            Expect(server.MoveRole(roleId, newPosition));

        await servers.SaveChangesAsync(ct);
        return role.ToDto();
    }

    private static void Expect(RoleChangeOutcome outcome)
    {
        if (outcome != RoleChangeOutcome.Done)
            throw new InvalidOperationException($"Role change unexpectedly refused: {outcome}.");
    }
}
