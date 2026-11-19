using Application.Common;
using Application.Servers;
using Contracts.Servers;
using DomainPermissions = Domain.Servers.Permissions;

namespace Application.Roles;

/// <summary>Creates a role on top of the existing ones (API-ROLE-02). Needs ManageServer.</summary>
public sealed class CreateRole(IServerRepository servers, TimeProvider clock)
{
    public async Task<Result<RoleDto>> HandleAsync(Guid userId, Guid serverId, CreateRoleRequest request, CancellationToken ct)
    {
        var loaded = await ManagedServer.LoadAsync(servers, serverId, userId, DomainPermissions.ManageServer, ct);
        if (!loaded.IsSuccess)
            return loaded.Error;

        var permissions = (DomainPermissions)(long)request.Permissions;
        var errors = new Dictionary<string, string[]>();
        RoleValidation.Name(errors, request.Name);
        RoleValidation.Color(errors, request.Color);
        RoleValidation.Permissions(errors, permissions);
        if (errors.Count > 0)
            return AppError.Validation(errors);

        var role = loaded.Value.CreateRole(request.Name, permissions, request.Color, clock.GetUtcNow());
        await servers.SaveChangesAsync(ct);
        return role.ToDto();
    }
}
