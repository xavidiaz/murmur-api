using Application.Common;
using Application.Roles;
using Application.Servers;
using Application.Tests.Servers;
using Contracts;
using Contracts.Servers;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using DomainPermissions = Domain.Servers.Permissions;
using DomainServer = Domain.Servers.Server;

namespace Application.Tests.Roles;

/// <summary>Manage roles — API-ROLE-02: the use cases, with the repository faked.</summary>
public sealed class ManageRolesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeServerRepository _servers = new();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Guid _owner = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();
    private readonly DomainServer _server;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ManageRolesTests()
    {
        _server = DomainServer.Create(_owner, "roles", null, Now);
        _server.AddMember(_member, Now);
        _servers.Added.Add(_server);
    }

    private Task<Result<RoleDto>> CreateAsync(string? name = "Mods", int? color = null, Permissions permissions = Permissions.None, Guid? by = null) =>
        new CreateRole(_servers, _clock).HandleAsync(by ?? _owner, _server.Id, new CreateRoleRequest(name!, color, permissions), Ct);

    private Task<Result<RoleDto>> UpdateAsync(Guid roleId, string? name = null, int? color = null, Permissions? permissions = null, int? position = null, Guid? by = null) =>
        new UpdateRole(_servers).HandleAsync(by ?? _owner, _server.Id, roleId, new UpdateRoleRequest(name, color, permissions, position), Ct);

    private Task<Result<bool>> DeleteAsync(Guid roleId, Guid? by = null) =>
        new DeleteRole(_servers).HandleAsync(by ?? _owner, _server.Id, roleId, Ct);

    private Guid EveryoneId => _server.Roles.Single(r => r.IsEveryone).Id;

    // ---- create ----

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task A_new_role_goes_on_top_of_the_existing_ones()
    {
        var first = (await CreateAsync(" Moderators ", 0x3498DB, Permissions.ManageMessages)).Value;
        var second = (await CreateAsync("Helpers")).Value;

        Assert.Equal(new RoleDto(first.Id, _server.Id, "Moderators", 0x3498DB, 1, Permissions.ManageMessages, false), first);
        Assert.Equal(2, second.Position);
        Assert.Equal(2, _servers.Saves);
    }

    [Theory]
    [Trait("Story", "API-ROLE-02")]
    [InlineData("name", "", null, Permissions.None)]
    [InlineData("name", "   ", null, Permissions.None)]
    [InlineData("name", null, null, Permissions.None)]
    [InlineData("color", "Mods", -1, Permissions.None)]
    [InlineData("color", "Mods", 0x1000000, Permissions.None)]
    [InlineData("permissions", "Mods", null, (Permissions)(1L << 40))]
    public async Task A_role_with_a_bad_field_is_rejected_on_its_own(string field, string? name, int? color, Permissions permissions)
    {
        var result = await CreateAsync(name, color, permissions);

        Assert.Equal([field], result.Error.Errors!.Keys);
        Assert.Equal(0, _servers.Saves);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task A_role_name_over_100_characters_is_rejected()
    {
        var result = await CreateAsync(new string('r', 101));

        Assert.Equal(["name"], result.Error.Errors!.Keys);
    }

    // ---- update ----

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Changing_only_a_roles_name_leaves_its_colour_and_permissions_alone()
    {
        var role = (await CreateAsync("Mods", 0x3498DB, Permissions.ManageMessages)).Value;

        var updated = (await UpdateAsync(role.Id, name: "Moderators")).Value;

        Assert.Equal(role with { Name = "Moderators" }, updated);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Everyone_cant_be_renamed_or_moved()
    {
        var rename = await UpdateAsync(EveryoneId, name: "everybody");
        var move = await UpdateAsync(EveryoneId, position: 1);

        Assert.Equal(RoleErrors.EveryoneRoleImmutable, rename.Error);
        Assert.Equal(ErrorCodes.EveryoneRoleImmutable, move.Error.Code);
        Assert.Equal(0, _servers.Saves);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Everyones_permissions_can_be_changed()
    {
        var updated = (await UpdateAsync(EveryoneId, permissions: Permissions.ViewChannel)).Value;

        Assert.Equal((Permissions.ViewChannel, 0, true), (updated.Permissions, updated.Position, updated.IsEveryone));
        Assert.Equal(DomainPermissions.ViewChannel, PermissionService.EffectivePermissions(_server, _member));
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Moving_a_role_renumbers_the_others()
    {
        var a = (await CreateAsync("a")).Value;
        var b = (await CreateAsync("b")).Value;
        var c = (await CreateAsync("c")).Value;

        var moved = (await UpdateAsync(c.Id, position: 1)).Value;

        Assert.Equal(1, moved.Position);
        Assert.Equal(["@everyone", "c", "a", "b"], _server.Roles.OrderBy(r => r.Position).Select(r => r.Name));
        Assert.NotEqual(a.Id, b.Id);
    }

    [Theory]
    [Trait("Story", "API-ROLE-02")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task A_position_outside_1_to_the_number_of_roles_is_rejected(int position)
    {
        var a = (await CreateAsync("a")).Value;
        await CreateAsync("b");

        var result = await UpdateAsync(a.Id, name: "renamed", position: position);

        Assert.Equal(["position"], result.Error.Errors!.Keys);
        Assert.Equal("a", _server.Roles.Single(r => r.Id == a.Id).Name); // nothing changed
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Several_bad_fields_are_reported_together_and_nothing_changes()
    {
        var a = (await CreateAsync("a", 0x123456)).Value;

        var result = await UpdateAsync(a.Id, name: " ", color: -5, permissions: (Permissions)(1L << 40), position: 9);

        Assert.Equal(["name", "color", "permissions", "position"], result.Error.Errors!.Keys);
        Assert.Equal(("a", (int?)0x123456), (_server.Roles.Single(r => r.Id == a.Id).Name, _server.Roles.Single(r => r.Id == a.Id).Color));
    }

    // ---- delete ----

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task I_can_delete_a_role_and_the_others_close_the_gap()
    {
        var a = (await CreateAsync("a")).Value;
        var b = (await CreateAsync("b")).Value;

        var result = await DeleteAsync(a.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _server.Roles.Single(r => r.Id == b.Id).Position);
        Assert.DoesNotContain(_server.Roles, r => r.Id == a.Id);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Everyone_cant_be_deleted()
    {
        var result = await DeleteAsync(EveryoneId);

        Assert.Equal(RoleErrors.EveryoneRoleImmutable, result.Error);
    }

    // ---- who may ----

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task A_regular_member_cant_manage_roles()
    {
        var role = (await CreateAsync()).Value;
        var saves = _servers.Saves;

        Assert.Equal(ServerErrors.Forbidden, (await CreateAsync(by: _member)).Error);
        Assert.Equal(ServerErrors.Forbidden, (await UpdateAsync(role.Id, name: "x", by: _member)).Error);
        Assert.Equal(ServerErrors.Forbidden, (await DeleteAsync(role.Id, by: _member)).Error);
        Assert.Equal(saves, _servers.Saves);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task A_member_whose_role_allows_managing_the_server_can_manage_roles()
    {
        var managers = (await CreateAsync("managers", permissions: Permissions.ManageServer)).Value;
        _server.AssignRole(_member, managers.Id);

        var result = await CreateAsync("made by a manager", by: _member);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Someone_outside_the_server_gets_the_same_answer_as_for_a_server_that_doesnt_exist()
    {
        var outsider = await CreateAsync(by: Guid.CreateVersion7());
        var missing = await new CreateRole(_servers, _clock).HandleAsync(_owner, Guid.CreateVersion7(), new CreateRoleRequest("x", null, Permissions.None), Ct);

        Assert.Equal(ServerErrors.NotFound, outsider.Error);
        Assert.Equal(ServerErrors.NotFound, missing.Error);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task A_role_that_doesnt_exist_isnt_found()
    {
        Assert.Equal(RoleErrors.NotFound, (await UpdateAsync(Guid.CreateVersion7(), name: "x")).Error);
        Assert.Equal(RoleErrors.NotFound, (await DeleteAsync(Guid.CreateVersion7())).Error);
    }

    // ---- list ----

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Members_see_the_roles_in_order_and_outsiders_dont()
    {
        await CreateAsync("a");
        await CreateAsync("b");

        var asMember = await new ListRoles(_servers).HandleAsync(_member, _server.Id, Ct);
        var asOutsider = await new ListRoles(_servers).HandleAsync(Guid.CreateVersion7(), _server.Id, Ct);

        Assert.Equal(["@everyone", "a", "b"], asMember.Value.Select(r => r.Name));
        Assert.Equal([0, 1, 2], asMember.Value.Select(r => r.Position));
        Assert.Equal(ServerErrors.NotFound, asOutsider.Error);
    }
}
