using Application.Servers;
using Domain.Servers;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Servers;

/// <summary>Working out what someone may do — API-ROLE-01: member roles stored and read back on a real Postgres.</summary>
public sealed class WorkingOutWhatSomeoneMayDoTests(TestDatabase database) : IClassFixture<TestDatabase>
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

    /// <summary>A server where <c>member</c> holds two roles and <c>other</c> holds none.</summary>
    private async Task<(Server Server, Guid Member, Guid Other, Role Moderator, Role Channels)> SavedServerAsync()
    {
        var owner = await NewUserAsync();
        var member = await NewUserAsync();
        var other = await NewUserAsync();
        var server = Server.Create(owner, "roles", null, Now);
        server.AddMember(member, Now);
        server.AddMember(other, Now);
        var moderator = server.CreateRole("moderator", Permissions.ManageMessages, 0x3498db, Now);
        var channels = server.CreateRole("channels", Permissions.ManageChannels, null, Now);
        server.AssignRole(member, moderator.Id);
        server.AssignRole(member, channels.Id);
        await using var db = database.CreateContext();
        await new ServerRepository(db).AddAsync(server, Ct);
        return (server, member, other, moderator, channels);
    }

    private static readonly Permissions MemberWithBothRoles =
        Role.EveryoneDefaultPermissions | Permissions.ManageMessages | Permissions.ManageChannels;

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public async Task A_members_roles_are_stored_and_loaded_back()
    {
        var (server, member, other, moderator, channels) = await SavedServerAsync();

        await using var db = database.CreateContext();
        var loaded = await db.Servers.Include(s => s.Roles).Include(s => s.Members).ThenInclude(m => m.RoleAssignments)
            .SingleAsync(s => s.Id == server.Id, Ct);

        Assert.Equal(new[] { moderator.Id, channels.Id }.Order(), loaded.Members.Single(m => m.UserId == member).RoleIds.Order());
        Assert.Empty(loaded.Members.Single(m => m.UserId == other).RoleIds);
        Assert.Equal(3, loaded.Roles.Count);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public async Task Opening_a_server_as_a_member_with_two_roles_is_still_one_query_and_counts_both_roles()
    {
        // Given
        var (server, member, other, _, _) = await SavedServerAsync();

        // When
        var commands = new CommandCounter();
        await using var db = database.CreateContext(commands);
        var repository = new ServerRepository(db);
        var asMember = await repository.FindForMemberAsync(server.Id, member, Ct);
        var queries = commands.Count;
        var asOther = await repository.FindForMemberAsync(server.Id, other, Ct);

        // Then
        Assert.Equal(1, queries);
        Assert.Equal(MemberWithBothRoles, PermissionService.EffectivePermissions(asMember!, member));
        Assert.Equal(Role.EveryoneDefaultPermissions, PermissionService.EffectivePermissions(asOther!, other));
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public async Task Server_access_for_a_member_with_two_roles_is_still_one_query_and_counts_both_roles()
    {
        // Given
        var (server, member, other, _, _) = await SavedServerAsync();

        // When
        var commands = new CommandCounter();
        await using var db = database.CreateContext(commands);
        var repository = new ServerRepository(db);
        var asMember = await repository.GetAccessAsync(server.Id, member, Ct);
        var queries = commands.Count;
        var asOther = await repository.GetAccessAsync(server.Id, other, Ct);

        // Then
        Assert.Equal(1, queries);
        Assert.Equal(MemberWithBothRoles, PermissionService.EffectivePermissions(asMember!, member));
        Assert.Empty(asOther!.MemberRolePermissions); // another member's roles don't leak in
        Assert.Equal(Role.EveryoneDefaultPermissions, PermissionService.EffectivePermissions(asOther, other));
        Assert.Equal(3, asMember!.MemberCount); // owner, member, other
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public async Task Deleting_a_role_or_a_membership_drops_the_assignments()
    {
        // Given
        var (server, member, _, moderator, channels) = await SavedServerAsync();

        // When: the moderator role is deleted
        await using (var db = database.CreateContext())
            await db.Database.ExecuteSqlAsync($"""DELETE FROM "Roles" WHERE "Id" = {moderator.Id}""", Ct);

        // Then
        await using (var db = database.CreateContext())
            Assert.Equal([channels.Id], await db.Set<MemberRole>().Where(a => a.UserId == member).Select(a => a.RoleId).ToListAsync(Ct));

        // When: the member is removed
        await using (var db = database.CreateContext())
            await db.Database.ExecuteSqlAsync(
                $"""DELETE FROM "ServerMembers" WHERE "ServerId" = {server.Id} AND "UserId" = {member}""", Ct);

        // Then
        await using (var db = database.CreateContext())
            Assert.False(await db.Set<MemberRole>().AnyAsync(a => a.ServerId == server.Id, Ct));
    }
}
