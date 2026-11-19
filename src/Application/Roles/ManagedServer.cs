using Application.Common;
using Application.Servers;
using Domain.Servers;

namespace Application.Roles;

/// <summary>Loads the whole server for a write that needs a permission.</summary>
internal static class ManagedServer
{
    /// <summary>
    /// The server, if <paramref name="userId"/> is a member with <paramref name="permission"/>. No such server and not a
    /// member are the same 404; a member without the permission gets 403.
    /// </summary>
    public static async Task<Result<Server>> LoadAsync(IServerRepository servers, Guid serverId, Guid userId,
        Permissions permission, CancellationToken ct)
    {
        var server = await servers.LoadAsync(serverId, ct);
        if (server is null || server.Members.All(m => m.UserId != userId))
            return ServerErrors.NotFound;
        if (!PermissionService.EffectivePermissions(server, userId).HasFlag(permission))
            return ServerErrors.Forbidden;
        return server;
    }
}
