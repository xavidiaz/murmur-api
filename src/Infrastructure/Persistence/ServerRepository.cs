using Application.Servers;
using Contracts.Users;
using Domain.Servers;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class ServerRepository(AppDbContext db) : IServerRepository
{
    public async Task AddAsync(Server server, CancellationToken ct)
    {
        db.Servers.Add(server);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>One query: <c>ServerMembers</c> (by the <c>UserId</c> index) joined to <c>Servers</c>.</summary>
    public async Task<IReadOnlyList<ServerSummary>> ListForMemberAsync(Guid userId, CancellationToken ct) =>
        await db.Set<ServerMember>()
            .Where(m => m.UserId == userId)
            .Join(db.Servers, m => m.ServerId, s => s.Id, (_, s) => s)
            .OrderBy(s => s.Id)
            .Select(s => new ServerSummary(s.Id, s.Name, s.IconUrl, s.OwnerId))
            .ToListAsync(ct);

    /// <summary>
    /// One round trip: the server, filtered on the caller's membership, with roles, channels and only the caller's
    /// own membership row (enough for <see cref="PermissionService"/> without loading every member). No tracking:
    /// the partial aggregate is a read model.
    /// </summary>
    public Task<Server?> FindForMemberAsync(Guid serverId, Guid userId, CancellationToken ct) =>
        db.Servers
            .AsNoTracking()
            .Where(s => s.Id == serverId && s.Members.Any(m => m.UserId == userId))
            .Include(s => s.Roles)
            .Include(s => s.Channels)
            .Include(s => s.Members.Where(m => m.UserId == userId))
                .ThenInclude(m => m.RoleAssignments)
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// The whole aggregate, tracked, for a write. Split into one query per collection so members × roles × channels
    /// don't multiply into one wide result.
    /// </summary>
    public Task<Server?> LoadAsync(Guid serverId, CancellationToken ct) =>
        db.Servers
            .Include(s => s.Roles)
            .Include(s => s.Channels)
            .Include(s => s.Members).ThenInclude(m => m.RoleAssignments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == serverId, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    /// <summary>One query: the roles in position order, only if the user is a member (null otherwise).</summary>
    public async Task<IReadOnlyList<Role>?> ListRolesForMemberAsync(Guid serverId, Guid userId, CancellationToken ct) =>
        await db.Servers
            .AsNoTracking()
            .Where(s => s.Id == serverId && s.Members.Any(m => m.UserId == userId))
            .Select(s => s.Roles.OrderBy(r => r.Position).ToList())
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// One query: the server's members joined to their user rows, with each member's role ids (by role position) as a
    /// correlated subquery, only if <paramref name="userId"/> is a member. A server always has at least its owner, so an
    /// empty result means "no such server or not a member" → null.
    /// </summary>
    public async Task<IReadOnlyList<MemberSummary>?> ListMembersForMemberAsync(Guid serverId, Guid userId, CancellationToken ct)
    {
        var members = db.Set<ServerMember>();
        var rows = await members
            .Where(m => m.ServerId == serverId && members.Any(me => me.ServerId == serverId && me.UserId == userId))
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { Member = m, User = u })
            .OrderBy(x => x.Member.JoinedAt).ThenBy(x => x.Member.UserId)
            .Select(x => new
            {
                x.User.Id, x.User.UserName, x.User.DisplayName, x.User.AvatarUrl,
                x.Member.Nickname, x.Member.JoinedAt,
                RoleIds = db.Set<Role>()
                    .Where(r => r.ServerId == serverId && x.Member.RoleAssignments.Any(a => a.RoleId == r.Id))
                    .OrderBy(r => r.Position)
                    .Select(r => r.Id)
                    .ToList(),
            })
            .ToListAsync(ct);
        if (rows.Count == 0)
            return null;
        return [.. rows.Select(r => new MemberSummary(new UserDto(r.Id, r.UserName!, r.DisplayName, r.AvatarUrl), r.Nickname, r.JoinedAt, r.RoleIds))];
    }

    /// <summary>One query: the server row filtered on membership, its @everyone permissions and a COUNT of members.</summary>
    public Task<ServerAccess?> GetAccessAsync(Guid serverId, Guid userId, CancellationToken ct) =>
        db.Servers
            .Where(s => s.Id == serverId && s.Members.Any(m => m.UserId == userId))
            .Select(s => new ServerAccess(
                s.Id, s.Name, s.IconUrl, s.OwnerId,
                s.Roles.Where(r => r.IsEveryone).Select(r => r.Permissions).Single(),
                s.Roles
                    .Where(r => s.Members.Any(m => m.UserId == userId && m.RoleAssignments.Any(a => a.RoleId == r.Id)))
                    .Select(r => r.Permissions)
                    .ToList(),
                s.Members.Count()))
            .SingleOrDefaultAsync(ct);
}
