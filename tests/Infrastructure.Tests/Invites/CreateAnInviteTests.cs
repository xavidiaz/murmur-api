using Domain.Invites;
using Domain.Servers;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Invites;

/// <summary>Create an invite — API-INV-02: invite storage and the server-access query, on a real Postgres.</summary>
public sealed class CreateAnInviteTests(TestDatabase database) : IClassFixture<TestDatabase>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> NewUserAsync()
    {
        await using var db = database.CreateContext();
        var id = Guid.CreateVersion7();
        var name = "u_" + Guid.NewGuid().ToString("N")[..12];
        db.Users.Add(new ApplicationUser { Id = id, UserName = name, NormalizedUserName = name.ToUpperInvariant(), DisplayName = name });
        await db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<Server> NewServerAsync(Guid owner)
    {
        var server = Server.Create(owner, "invite test", "https://example.test/i.png", Now);
        await using var db = database.CreateContext();
        await new ServerRepository(db).AddAsync(server, Ct);
        return server;
    }

    private async Task AddMemberAsync(Guid serverId, Guid userId)
    {
        await using var db = database.CreateContext();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO "ServerMembers" ("ServerId", "UserId", "JoinedAt") VALUES ({serverId}, {userId}, {Now})""", Ct);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task An_invite_is_saved_and_a_code_collision_is_reported_without_saving_anything()
    {
        // Given
        var owner = await NewUserAsync();
        var server = await NewServerAsync(owner);
        var first = Invite.Create(server.Id, owner, Now, Now.AddHours(24), maxUses: 3);
        var clash = Invite.Create(server.Id, owner, Now, expiresAt: null, maxUses: null);
        typeof(Invite).GetProperty(nameof(Invite.Code))!.SetValue(clash, first.Code); // force the astronomically rare case
        var retry = Invite.Create(server.Id, owner, Now, expiresAt: null, maxUses: null);

        bool savedFirst;
        await using (var earlier = database.CreateContext()) // an earlier request already stored the code
            savedFirst = await new InviteRepository(earlier).TryAddAsync(first, Ct);

        // When: this request's context hits the collision, then retries with a fresh code, as the use case does
        await using var db = database.CreateContext();
        var repository = new InviteRepository(db);
        var savedClash = await repository.TryAddAsync(clash, Ct);
        var savedRetry = await repository.TryAddAsync(retry, Ct);

        // Then
        Assert.Equal((true, false, true), (savedFirst, savedClash, savedRetry));
        await using var check = database.CreateContext();
        var stored = await check.Invites.Where(i => i.ServerId == server.Id).OrderBy(i => i.CreatedAt).ToListAsync(Ct);
        Assert.Equal(2, stored.Count);
        var kept = Assert.Single(stored, i => i.Code == first.Code);
        Assert.Equal((Now.AddHours(24), (int?)3, 0), (kept.ExpiresAt, kept.MaxUses, kept.Uses)); // the clash didn't overwrite it
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task Server_access_is_one_query_with_the_member_count_and_hidden_from_outsiders()
    {
        // Given
        var owner = await NewUserAsync();
        var member = await NewUserAsync();
        var server = await NewServerAsync(owner);
        await AddMemberAsync(server.Id, member);

        // When
        var commands = new CommandCounter();
        await using var db = database.CreateContext(commands);
        var repository = new ServerRepository(db);
        var access = await repository.GetAccessAsync(server.Id, member, Ct);
        var queries = commands.Count;
        var outsider = await repository.GetAccessAsync(server.Id, Guid.CreateVersion7(), Ct);
        var missing = await repository.GetAccessAsync(Guid.CreateVersion7(), owner, Ct);

        // Then
        Assert.NotNull(access);
        Assert.Equal((server.Id, "invite test", "https://example.test/i.png", owner, Role.EveryoneDefaultPermissions, 2),
            (access.ServerId, access.Name, access.IconUrl, access.OwnerId, access.EveryonePermissions, access.MemberCount));
        Assert.Equal(1, queries);
        Assert.Null(outsider);
        Assert.Null(missing);
    }
}
