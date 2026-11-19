using Application.Servers;
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
            .SingleOrDefaultAsync(ct);

    /// <summary>One query: the server row filtered on membership, its @everyone permissions and a COUNT of members.</summary>
    public Task<ServerAccess?> GetAccessAsync(Guid serverId, Guid userId, CancellationToken ct) =>
        db.Servers
            .Where(s => s.Id == serverId && s.Members.Any(m => m.UserId == userId))
            .Select(s => new ServerAccess(
                s.Id, s.Name, s.IconUrl, s.OwnerId,
                s.Roles.Where(r => r.IsEveryone).Select(r => r.Permissions).Single(),
                new List<Domain.Servers.Permissions>(),
                s.Members.Count()))
            .SingleOrDefaultAsync(ct);
}
