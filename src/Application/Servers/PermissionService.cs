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

    public static Permissions EffectivePermissions(Server server, Guid userId) =>
        EffectivePermissions(
            server.OwnerId,
            server.Roles.Single(r => r.IsEveryone).Permissions,
            isMember: server.Members.Any(m => m.UserId == userId),
            userId);

    /// <summary>For a member, from a <see cref="ServerAccess"/> read model.</summary>
    public static Permissions EffectivePermissions(ServerAccess access, Guid userId) =>
        EffectivePermissions(access.OwnerId, access.EveryonePermissions, isMember: true, userId);

    /// <summary>The single rule: what <paramref name="userId"/> may do, given everything that decides it.</summary>
    public static Permissions Compute(Guid ownerId, Guid userId, bool isMember, Permissions everyone,
        IEnumerable<Permissions> memberRolePermissions) => throw new NotImplementedException();

    private static Permissions EffectivePermissions(Guid ownerId, Permissions everyone, bool isMember, Guid userId)
    {
        if (ownerId == userId)
            return All;
        if (!isMember)
            return Permissions.None;
        return everyone.HasFlag(Permissions.Administrator) ? All : everyone;
    }
}
