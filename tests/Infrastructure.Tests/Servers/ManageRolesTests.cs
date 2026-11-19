using Domain.Servers;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Servers;

/// <summary>Manage roles — API-ROLE-02: changing a stored server through the full-aggregate write path.</summary>
public sealed class ManageRolesTests(TestDatabase database) : IClassFixture<TestDatabase>
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

    private async Task<(Guid ServerId, Guid Member)> SavedServerAsync()
    {
        var owner = await NewUserAsync();
        var member = await NewUserAsync();
        var server = Server.Create(owner, "roles", null, Now);
        server.AddMember(member, Now);
        await using var db = database.CreateContext();
        await new ServerRepository(db).AddAsync(server, Ct);
        return (server.Id, member);
    }

    private async Task<string[]> RoleOrderAsync(Guid serverId)
    {
        await using var db = database.CreateContext();
        return [.. await db.Servers.Where(s => s.Id == serverId)
            .SelectMany(s => s.Roles).OrderBy(r => r.Position).Select(r => r.Position + ":" + r.Name).ToListAsync(Ct)];
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Roles_created_moved_and_deleted_on_a_loaded_server_are_saved()
    {
        // Given: three roles, a member holding the one that will be deleted
        var (serverId, member) = await SavedServerAsync();
        Guid a, c;
        await using (var db = database.CreateContext())
        {
            var repository = new ServerRepository(db);
            var server = (await repository.LoadAsync(serverId, Ct))!;
            a = server.CreateRole("a", Permissions.ManageMessages, 0x123456, Now).Id;
            server.CreateRole("b", Permissions.None, null, Now);
            c = server.CreateRole("c", Permissions.None, null, Now).Id;
            server.AssignRole(member, a);
            await repository.SaveChangesAsync(Ct);
        }
        Assert.Equal(["0:@everyone", "1:a", "2:b", "3:c"], await RoleOrderAsync(serverId));

        // When: c moves to the top, then a is renamed and deleted in a later request
        await using (var db = database.CreateContext())
        {
            var repository = new ServerRepository(db);
            var server = (await repository.LoadAsync(serverId, Ct))!;
            Assert.Equal(RoleChangeOutcome.Done, server.MoveRole(c, 1));
            Assert.Equal(RoleChangeOutcome.Done, server.UpdateRole(a, "renamed", null, null));
            await repository.SaveChangesAsync(Ct);
        }
        Assert.Equal(["0:@everyone", "1:c", "2:renamed", "3:b"], await RoleOrderAsync(serverId));

        await using (var db = database.CreateContext())
        {
            var repository = new ServerRepository(db);
            var server = (await repository.LoadAsync(serverId, Ct))!;
            Assert.Equal([a], server.Members.Single(m => m.UserId == member).RoleIds); // the full aggregate came back
            Assert.Equal(RoleChangeOutcome.Done, server.DeleteRole(a));
            await repository.SaveChangesAsync(Ct);
        }

        // Then
        Assert.Equal(["0:@everyone", "1:c", "2:b"], await RoleOrderAsync(serverId));
        await using var check = database.CreateContext();
        Assert.False(await check.Set<MemberRole>().AnyAsync(x => x.RoleId == a, Ct));
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Everyones_new_permissions_are_saved()
    {
        var (serverId, _) = await SavedServerAsync();
        await using (var db = database.CreateContext())
        {
            var repository = new ServerRepository(db);
            var server = (await repository.LoadAsync(serverId, Ct))!;
            server.UpdateRole(server.Roles.Single(r => r.IsEveryone).Id, null, null, Permissions.ViewChannel);
            await repository.SaveChangesAsync(Ct);
        }

        await using var check = database.CreateContext();
        Assert.Equal(Permissions.ViewChannel,
            await check.Servers.Where(s => s.Id == serverId).SelectMany(s => s.Roles).Where(r => r.IsEveryone).Select(r => r.Permissions).SingleAsync(Ct));
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Listing_roles_is_one_query_for_members_only()
    {
        // Given
        var (serverId, member) = await SavedServerAsync();
        await using (var db = database.CreateContext())
        {
            var repository = new ServerRepository(db);
            var server = (await repository.LoadAsync(serverId, Ct))!;
            server.CreateRole("b", Permissions.None, null, Now);
            server.CreateRole("a", Permissions.None, null, Now);
            await repository.SaveChangesAsync(Ct);
        }

        // When
        var commands = new CommandCounter();
        await using var read = database.CreateContext(commands);
        var repo = new ServerRepository(read);
        var asMember = await repo.ListRolesForMemberAsync(serverId, member, Ct);
        var queries = commands.Count;
        var asOutsider = await repo.ListRolesForMemberAsync(serverId, Guid.CreateVersion7(), Ct);

        // Then
        Assert.Equal(1, queries);
        Assert.Equal(["@everyone", "b", "a"], asMember!.Select(r => r.Name));
        Assert.Null(asOutsider);
    }
}

/// <summary>Give members roles — API-ROLE-03: the members list is one query with users and role ids.</summary>
public sealed class GiveMembersRolesTests(TestDatabase database) : IClassFixture<TestDatabase>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> NewUserAsync(string displayName)
    {
        await using var db = database.CreateContext();
        var id = Guid.CreateVersion7();
        var name = "u_" + Guid.NewGuid().ToString("N")[..12];
        db.Users.Add(new ApplicationUser { Id = id, UserName = name, NormalizedUserName = name.ToUpperInvariant(), DisplayName = displayName });
        await db.SaveChangesAsync(Ct);
        return id;
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task The_members_list_is_one_query_in_join_order_with_users_and_their_roles()
    {
        // Given: owner, then bob (two roles), then carol (none)
        var owner = await NewUserAsync("Owner");
        var bob = await NewUserAsync("Bob");
        var carol = await NewUserAsync("Carol");
        var server = Server.Create(owner, "members", null, Now);
        server.AddMember(carol, Now.AddMinutes(2));
        server.AddMember(bob, Now.AddMinutes(1));
        var mods = server.CreateRole("mods", Permissions.ManageMessages, null, Now);
        var helpers = server.CreateRole("helpers", Permissions.None, null, Now);
        server.AssignRole(bob, helpers.Id);
        server.AssignRole(bob, mods.Id);
        await using (var db = database.CreateContext())
            await new ServerRepository(db).AddAsync(server, Ct);

        // When
        var commands = new CommandCounter();
        await using var read = database.CreateContext(commands);
        var repository = new ServerRepository(read);
        var members = await repository.ListMembersForMemberAsync(server.Id, carol, Ct);
        var queries = commands.Count;
        var asOutsider = await repository.ListMembersForMemberAsync(server.Id, Guid.CreateVersion7(), Ct);
        var missing = await repository.ListMembersForMemberAsync(Guid.CreateVersion7(), owner, Ct);

        // Then
        Assert.Equal(1, queries);
        Assert.NotNull(members);
        Assert.Equal([owner, bob, carol], members.Select(m => m.User.Id));
        Assert.Equal(["Owner", "Bob", "Carol"], members.Select(m => m.User.DisplayName));
        Assert.Equal([Now, Now.AddMinutes(1), Now.AddMinutes(2)], members.Select(m => m.JoinedAt));
        Assert.Equal([mods.Id, helpers.Id], members[1].RoleIds); // by role position, never @everyone
        Assert.Empty(members[0].RoleIds);
        Assert.Null(asOutsider);
        Assert.Null(missing);
    }
}
