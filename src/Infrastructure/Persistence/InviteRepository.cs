using Application.Invites;
using Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence;

public sealed class InviteRepository(AppDbContext db) : IInviteRepository
{
    private const string PrimaryKey = "PK_Invites";

    public async Task<bool> TryAddAsync(Invite invite, CancellationToken ct)
    {
        var entry = db.Invites.Add(invite);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
                                          { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: PrimaryKey })
        {
            entry.State = EntityState.Detached; // forget it so the retry starts clean
            return false;
        }
    }
}
