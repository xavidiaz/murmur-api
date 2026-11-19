using Application.Common;
using Application.Roles;
using Application.Servers;
using Application.Tests.Servers;
using Contracts;
using Xunit;
using DomainPermissions = Domain.Servers.Permissions;
using DomainServer = Domain.Servers.Server;
using Role = Domain.Servers.Role;

namespace Application.Tests.Roles;

/// <summary>Give members roles — API-ROLE-03: assigning and unassigning, with the repository faked.</summary>
public sealed class GiveMembersRolesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeServerRepository _servers = new();
    private readonly Guid _owner = Guid.CreateVersion7();
    private readonly Guid _bob = Guid.CreateVersion7();
    private readonly Guid _carol = Guid.CreateVersion7();
    private readonly DomainServer _server;
    private readonly Role _mods;
    private readonly Role _helpers;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GiveMembersRolesTests()
    {
        _server = DomainServer.Create(_owner, "roles", null, Now);
        _server.AddMember(_bob, Now.AddMinutes(1));
        _server.AddMember(_carol, Now.AddMinutes(2));
        _mods = _server.CreateRole("mods", DomainPermissions.ManageMessages, null, Now);
        _helpers = _server.CreateRole("helpers", DomainPermissions.ManageChannels, null, Now);
        _servers.Added.Add(_server);
    }

    private Task<Result<bool>> AssignAsync(Guid userId, Guid roleId, Guid? by = null, Guid? serverId = null) =>
        new AssignRole(_servers).HandleAsync(by ?? _owner, serverId ?? _server.Id, userId, roleId, Ct);

    private Task<Result<bool>> UnassignAsync(Guid userId, Guid roleId, Guid? by = null) =>
        new UnassignRole(_servers).HandleAsync(by ?? _owner, _server.Id, userId, roleId, Ct);

    private IReadOnlyCollection<Guid> RolesOf(Guid userId) => _server.Members.Single(m => m.UserId == userId).RoleIds;

    private Guid EveryoneId => _server.Roles.Single(r => r.IsEveryone).Id;

    // ---- assign ----

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Giving_a_member_a_role_shows_up_on_their_profile_and_giving_it_twice_is_harmless()
    {
        var first = await AssignAsync(_bob, _mods.Id);
        var second = await AssignAsync(_bob, _mods.Id);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal([_mods.Id], RolesOf(_bob));
        Assert.Equal(DomainPermissions.ManageMessages | Role.EveryoneDefaultPermissions, PermissionService.EffectivePermissions(_server, _bob));
        Assert.Equal(1, _servers.Saves); // the second one changed nothing
    }

    [Theory]
    [Trait("Story", "API-ROLE-03")]
    [InlineData("a role from another server")]
    [InlineData("a role that doesnt exist")]
    public async Task A_role_from_another_server_or_one_that_doesnt_exist_isnt_found(string what)
    {
        var otherServer = DomainServer.Create(_owner, "other", null, Now);
        var roleId = what == "a role from another server"
            ? otherServer.CreateRole("theirs", DomainPermissions.None, null, Now).Id
            : Guid.CreateVersion7();

        var result = await AssignAsync(_bob, roleId);

        Assert.Equal(RoleErrors.NotFound, result.Error);
        Assert.Empty(RolesOf(_bob));
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Someone_who_isnt_a_member_isnt_found()
    {
        var result = await AssignAsync(Guid.CreateVersion7(), _mods.Id);

        Assert.Equal(ServerErrors.MemberNotFound, result.Error);
        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Everyone_cant_be_given_out()
    {
        var result = await AssignAsync(_bob, EveryoneId);

        Assert.Equal(RoleErrors.EveryoneRoleImmutable, result.Error);
        Assert.Equal(ErrorCodes.EveryoneRoleImmutable, result.Error.Code);
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task A_regular_member_cant_give_out_roles_not_even_to_themselves()
    {
        Assert.Equal(ServerErrors.Forbidden, (await AssignAsync(_bob, _mods.Id, by: _bob)).Error);
        Assert.Equal(ServerErrors.Forbidden, (await AssignAsync(_carol, _mods.Id, by: _bob)).Error);
        Assert.Equal(0, _servers.Saves);
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Someone_outside_the_server_gets_the_same_answer_as_for_a_server_that_doesnt_exist()
    {
        Assert.Equal(ServerErrors.NotFound, (await AssignAsync(_bob, _mods.Id, by: Guid.CreateVersion7())).Error);
        Assert.Equal(ServerErrors.NotFound, (await AssignAsync(_bob, _mods.Id, serverId: Guid.CreateVersion7())).Error);
    }

    // ---- unassign: the same rules ----

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Taking_a_role_away_removes_it_and_keeps_the_others()
    {
        await AssignAsync(_bob, _mods.Id);
        await AssignAsync(_bob, _helpers.Id);

        var result = await UnassignAsync(_bob, _mods.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal([_helpers.Id], RolesOf(_bob));
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Taking_away_a_role_the_member_doesnt_hold_succeeds_and_changes_nothing()
    {
        var result = await UnassignAsync(_bob, _mods.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _servers.Saves);
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Taking_away_another_servers_role_or_from_a_non_member_isnt_found()
    {
        var theirs = DomainServer.Create(_owner, "other", null, Now).CreateRole("theirs", DomainPermissions.None, null, Now);

        Assert.Equal(RoleErrors.NotFound, (await UnassignAsync(_bob, theirs.Id)).Error);
        Assert.Equal(ServerErrors.MemberNotFound, (await UnassignAsync(Guid.CreateVersion7(), _mods.Id)).Error);
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Everyone_cant_be_taken_away()
    {
        Assert.Equal(RoleErrors.EveryoneRoleImmutable, (await UnassignAsync(_bob, EveryoneId)).Error);
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task A_regular_member_cant_take_roles_away()
    {
        await AssignAsync(_carol, _mods.Id);

        Assert.Equal(ServerErrors.Forbidden, (await UnassignAsync(_carol, _mods.Id, by: _bob)).Error);
        Assert.Equal([_mods.Id], RolesOf(_carol));
    }

    // ---- members list ----

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Members_see_everyone_in_join_order_with_their_roles_but_not_everyone_as_a_role()
    {
        // Given
        await AssignAsync(_carol, _helpers.Id);
        await AssignAsync(_carol, _mods.Id);

        // When
        var members = (await new ListMembers(_servers).HandleAsync(_bob, _server.Id, Ct)).Value;

        // Then
        Assert.Equal([_owner, _bob, _carol], members.Select(m => m.User.Id));
        Assert.Equal([Now, Now.AddMinutes(1), Now.AddMinutes(2)], members.Select(m => m.JoinedAt));
        Assert.Equal([_mods.Id, _helpers.Id], members[2].RoleIds); // by role position
        Assert.Empty(members[0].RoleIds);
        Assert.DoesNotContain(EveryoneId, members.SelectMany(m => m.RoleIds));
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Outsiders_cant_see_the_member_list()
    {
        var result = await new ListMembers(_servers).HandleAsync(Guid.CreateVersion7(), _server.Id, Ct);

        Assert.Equal(ServerErrors.NotFound, result.Error);
    }
}
