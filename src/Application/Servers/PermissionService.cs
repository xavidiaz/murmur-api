using Domain.Servers;

namespace Application.Servers;

/// <summary>
/// Works out what a user may do in a server. Minimal for now (API-SRV-02): the owner has every permission, a member
/// has the <c>@everyone</c> role's permissions (everything if that includes <see cref="Permissions.Administrator"/>),
/// and a non-member has none. API-ROLE-01 extends this with the member's own roles.
/// </summary>
public static class PermissionService
{
    public static readonly Permissions All =
        Enum.GetValues<Permissions>().Aggregate(Permissions.None, (all, p) => all | p);

    public static Permissions EffectivePermissions(Server server, Guid userId)
    {
        if (server.OwnerId == userId)
            return All;
        if (server.Members.All(m => m.UserId != userId))
            return Permissions.None;

        var permissions = server.Roles.Single(r => r.IsEveryone).Permissions;
        return permissions.HasFlag(Permissions.Administrator) ? All : permissions;
    }
}
