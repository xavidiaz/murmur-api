using Domain.Servers;

namespace Application.Servers;

/// <summary>
/// The one place that works out what a user may do in a server (API-ROLE-01). The owner may do everything. A member
/// may do what <c>@everyone</c> allows plus what each of their roles allows (the union); if that includes
/// <see cref="Permissions.Administrator"/>, everything. Someone who isn't a member may do nothing.
/// </summary>
public static class PermissionService
{
    public static readonly Permissions All = PermissionFlags.All;

    /// <summary>The rule itself. Every other overload only gathers its inputs.</summary>
    public static Permissions Compute(Guid ownerId, Guid userId, bool isMember, Permissions everyone,
        IEnumerable<Permissions> memberRolePermissions)
    {
        if (userId == ownerId)
            return All;
        if (!isMember)
            return Permissions.None;

        var granted = memberRolePermissions.Aggregate(everyone, (all, p) => all | p);
        return granted.HasFlag(Permissions.Administrator) ? All : granted;
    }

    /// <summary>From the aggregate (or a read model of it that includes the user's membership and roles).</summary>
    public static Permissions EffectivePermissions(Server server, Guid userId)
    {
        var member = server.Members.FirstOrDefault(m => m.UserId == userId);
        var held = member?.RoleIds ?? [];
        return Compute(
            server.OwnerId, userId, isMember: member is not null,
            server.Roles.Single(r => r.IsEveryone).Permissions,
            server.Roles.Where(r => held.Contains(r.Id)).Select(r => r.Permissions));
    }

    /// <summary>For a member, from a <see cref="ServerAccess"/> read model.</summary>
    public static Permissions EffectivePermissions(ServerAccess access, Guid userId) =>
        Compute(access.OwnerId, userId, isMember: true, access.EveryonePermissions, access.MemberRolePermissions);
}
