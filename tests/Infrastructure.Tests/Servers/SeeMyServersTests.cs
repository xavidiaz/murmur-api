using Application.Servers;
using Domain.Servers;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Servers;

/// <summary>See my servers — API-SRV-03: membership-gated queries on a real Postgres, one round trip each.</summary>
public sealed class SeeMyServersTests(TestDatabase database) : IClassFixture<TestDatabase>
{
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

    private async Task<Server> CreateServerAsync(Guid owner, string name)
    {
        var server = Server.Create(owner, name, null, DateTimeOffset.UtcNow);
        await using var db = database.CreateContext();
        await new ServerRepository(db).AddAsync(server, Ct);
        return server;
    }

    /// <summary>Until invites exist there's no domain operation for joining, so add the membership row directly.</summary>
    private async Task AddMemberAsync(Guid serverId, Guid userId)
    {
        await using var db = database.CreateContext();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO "ServerMembers" ("ServerId", "UserId", "JoinedAt") VALUES ({serverId}, {userId}, {DateTimeOffset.UtcNow})""", Ct);
    }

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task My_list_is_one_query_and_shows_only_the_servers_Im_in_in_creation_order()
    {
        // Given
        var alice = await NewUserAsync();
        var bob = await NewUserAsync();
        var first = await CreateServerAsync(alice, "first");
        var bobs = await CreateServerAsync(bob, "bob's");
        var second = await CreateServerAsync(alice, "second");
        await AddMemberAsync(bobs.Id, alice); // alice joins bob's server after creating "first"

        // When
        var commands = new CommandCounter();
        await using var db = database.CreateContext(commands);
        var mine = await new ServerRepository(db).ListForMemberAsync(alice, Ct);

        // Then
        Assert.Equal([first.Id, bobs.Id, second.Id], mine.Select(s => s.Id));
        Assert.Equal(new ServerSummary(bobs.Id, "bob's", null, bob), mine[1]);
        Assert.Equal(1, commands.Count);
    }

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task Opening_a_server_is_one_round_trip_with_its_roles_and_channels()
    {
        // Given
        var alice = await NewUserAsync();
        var server = await CreateServerAsync(alice, "mine");

        // When
        var commands = new CommandCounter();
        await using var db = database.CreateContext(commands);
        var loaded = await new ServerRepository(db).FindForMemberAsync(server.Id, alice, Ct);

        // Then
        Assert.NotNull(loaded);
        Assert.Single(loaded.Roles, r => r.IsEveryone);
        Assert.Single(loaded.Channels, c => c.Name == "general");
        Assert.Equal(1, commands.Count);
    }

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task A_plain_member_can_open_the_server_and_gets_the_everyone_permissions()
    {
        // Given
        var owner = await NewUserAsync();
        var member = await NewUserAsync();
        var server = await CreateServerAsync(owner, "shared");
        await AddMemberAsync(server.Id, member);

        // When
        await using var db = database.CreateContext();
        var loaded = await new ServerRepository(db).FindForMemberAsync(server.Id, member, Ct);

        // Then
        Assert.NotNull(loaded);
        Assert.Equal(member, Assert.Single(loaded.Members).UserId); // only the caller's own membership is loaded
        Assert.Equal(Role.EveryoneDefaultPermissions, PermissionService.EffectivePermissions(loaded, member));
    }

    [Fact]
    [Trait("Story", "API-SRV-03")]
    public async Task A_server_Im_not_in_comes_back_exactly_like_one_that_doesnt_exist()
    {
        // Given
        var alice = await NewUserAsync();
        var bobs = await CreateServerAsync(await NewUserAsync(), "bob's");

        // When
        await using var db = database.CreateContext();
        var repository = new ServerRepository(db);
        var notMine = await repository.FindForMemberAsync(bobs.Id, alice, Ct);
        var missing = await repository.FindForMemberAsync(Guid.NewGuid(), alice, Ct);

        // Then
        Assert.Null(notMine);
        Assert.Null(missing);
    }
}
