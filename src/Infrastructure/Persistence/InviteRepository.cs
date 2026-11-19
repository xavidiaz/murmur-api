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

    /// <summary>One query: the invite joined to its server, with a COUNT of the server's members.</summary>
    public async Task<InvitePreview?> FindPreviewAsync(string code, CancellationToken ct)
    {
        var row = await db.Invites
            .AsNoTracking()
            .Where(i => i.Code == code)
            .Join(db.Servers, i => i.ServerId, s => s.Id, (i, s) => new { Invite = i, s.Name, s.IconUrl, MemberCount = s.Members.Count() })
            .SingleOrDefaultAsync(ct);
        return row is null ? null : new InvitePreview(row.Invite, row.Name, row.IconUrl, row.MemberCount);
    }
}
