using Application.Invites;
using Contracts.Servers;
using Domain.Invites;
using Domain.Servers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence;

public sealed class InviteRepository(AppDbContext db) : IInviteRepository
{
    private const string PrimaryKey = "PK_Invites";
    private const string MemberPrimaryKey = "PK_ServerMembers";

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

    /// <summary>One query: the invite joined to its server, with an EXISTS for the caller's membership.</summary>
    public async Task<JoinTarget?> FindForJoinAsync(string code, Guid userId, CancellationToken ct)
    {
        var row = await db.Invites
            .AsNoTracking()
            .Where(i => i.Code == code)
            .Join(db.Servers, i => i.ServerId, s => s.Id, (i, s) => new
            {
                Invite = i, s.Id, s.Name, s.IconUrl, s.OwnerId,
                IsMember = s.Members.Any(m => m.UserId == userId),
            })
            .SingleOrDefaultAsync(ct);
        return row is null ? null : new JoinTarget(row.Invite, new ServerDto(row.Id, row.Name, row.IconUrl, row.OwnerId), row.IsMember);
    }

    /// <remarks>
    /// <para>The use is counted by a conditional <c>UPDATE … SET "Uses" = "Uses" + 1 WHERE "Code" = @c AND ("MaxUses" IS
    /// NULL OR "Uses" &lt; "MaxUses") AND ("ExpiresAt" IS NULL OR "ExpiresAt" &gt; @now)</c>. Concurrent redeemers queue
    /// on the invite's row lock and each re-checks the condition after the one before commits, so the limit can't be
    /// overshot. If the update matches no row, nothing has changed.</para>
    /// <para>The member is inserted in the same transaction. If the same user is joining concurrently, that insert
    /// waits for the other transaction and then hits <c>PK_ServerMembers</c>; rolling back undoes this request's use
    /// as well, so one user joining twice at once counts exactly one use.</para>
    /// </remarks>
    public async Task<RedeemOutcome> TryRedeemAsync(string code, ServerMember member, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var counted = await db.Invites
            .Where(i => i.Code == code
                        && (i.MaxUses == null || i.Uses < i.MaxUses)
                        && (i.ExpiresAt == null || i.ExpiresAt > now))
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Uses, i => i.Uses + 1), ct);
        if (counted != 1)
            return RedeemOutcome.NotUsable; // disposing the transaction rolls back (nothing was written anyway)

        var entry = db.Set<ServerMember>().Add(member);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
                                          { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: MemberPrimaryKey })
        {
            entry.State = EntityState.Detached;
            return RedeemOutcome.AlreadyMember; // disposing the transaction rolls back the use too
        }

        await transaction.CommitAsync(ct);
        return RedeemOutcome.Joined;
    }
}
