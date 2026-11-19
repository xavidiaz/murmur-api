using Application.Servers;
using Domain.Servers;

namespace Infrastructure.Persistence;

public sealed class ServerRepository(AppDbContext db) : IServerRepository
{
    public async Task AddAsync(Server server, CancellationToken ct)
    {
        db.Servers.Add(server);
        await db.SaveChangesAsync(ct);
    }
}
