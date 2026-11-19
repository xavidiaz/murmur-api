namespace Api.AcceptanceTests.Roles;

/// <summary>Manage roles — API-ROLE-02. As a manager, I want to create, edit and delete roles.</summary>
public sealed class ManageRolesTests(PostgresFixture db) : AcceptanceTest(db)
{
    private const int Blue = 0x3498DB;
    private const int Red = 0xE74C3C;

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task A_new_role_goes_on_top_of_the_existing_ones()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var everyone = Assert.Single(server.Roles);

        // When
        var firstResponse = await owner.PostAsync(ApiRoutes.Servers.Roles(server.Id),
            new CreateRoleRequest("Moderators", Blue, Permissions.ManageMessages | Permissions.ManageChannels));
        var first = await ApiClient.ReadAsync<RoleDto>(firstResponse, HttpStatusCode.Created);
        var second = await owner.CreateRoleAsync(server.Id, "Helpers", null, Permissions.ViewChannel);

        // Then
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal(server.Id, first.ServerId);
        Assert.Equal("Moderators", first.Name);
        Assert.Equal(Blue, first.Color);
        Assert.Equal(Permissions.ManageMessages | Permissions.ManageChannels, first.Permissions);
        Assert.False(first.IsEveryone);
        Assert.Equal(1, first.Position);
        Assert.Equal(2, second.Position);
        Assert.Null(second.Color);

        var listed = await owner.GetAsync<RoleDto[]>(ApiRoutes.Servers.Roles(server.Id));
        Assert.Equal([everyone, first, second], listed);
        var detail = await owner.GetAsync<ServerDetailDto>(ApiRoutes.Servers.Item(server.Id));
        Assert.Equal([everyone, first, second], detail.Roles);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Changing_only_a_roles_name_leaves_its_colour_and_permissions_alone()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var role = await owner.CreateRoleAsync(server.Id, "Mods", Blue, Permissions.ManageMessages);

        // When
        var response = await owner.PatchAsync(ApiRoutes.Servers.Role(server.Id, role.Id),
            new UpdateRoleRequest("Moderators", null, null, null));

        // Then
        var updated = await ApiClient.ReadAsync<RoleDto>(response);
        Assert.Equal(role with { Name = "Moderators" }, updated);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task I_can_change_a_roles_colour_and_permissions()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var role = await owner.CreateRoleAsync(server.Id, "Mods", Blue, Permissions.ManageMessages);

        // When
        var response = await owner.PatchAsync(ApiRoutes.Servers.Role(server.Id, role.Id),
            new UpdateRoleRequest(null, Red, Permissions.ManageMessages | Permissions.CreateInvite, null));

        // Then
        var updated = await ApiClient.ReadAsync<RoleDto>(response);
        Assert.Equal(role with { Color = Red, Permissions = Permissions.ManageMessages | Permissions.CreateInvite }, updated);
        var listed = await owner.GetAsync<RoleDto[]>(ApiRoutes.Servers.Roles(server.Id));
        Assert.Contains(updated, listed);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task I_can_delete_a_role()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var role = await owner.CreateRoleAsync(server.Id);

        // When
        var response = await owner.DeleteAsync(ApiRoutes.Servers.Role(server.Id, role.Id));

        // Then
        await ApiClient.AssertStatusAsync(response, HttpStatusCode.NoContent);
        var listed = await owner.GetAsync<RoleDto[]>(ApiRoutes.Servers.Roles(server.Id));
        Assert.DoesNotContain(listed, r => r.Id == role.Id);
        Assert.Contains(listed, r => r.IsEveryone);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Everyone_cant_be_renamed()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var everyone = Assert.Single(server.Roles);

        // When
        var response = await owner.PatchAsync(ApiRoutes.Servers.Role(server.Id, everyone.Id),
            new UpdateRoleRequest("everybody", null, null, null));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.EveryoneRoleImmutable);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Everyone_cant_be_deleted()
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var everyone = Assert.Single(server.Roles);

        // When
        var response = await owner.DeleteAsync(ApiRoutes.Servers.Role(server.Id, everyone.Id));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.EveryoneRoleImmutable);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Everyones_permissions_can_be_changed_and_apply_to_every_member()
    {
        // Given
        using var owner = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var everyone = Assert.Single(server.Roles);
        await bob.JoinAsync(owner, server.Id);
        const Permissions readOnly = Permissions.ViewChannel;

        // When
        var response = await owner.PatchAsync(ApiRoutes.Servers.Role(server.Id, everyone.Id),
            new UpdateRoleRequest(null, null, readOnly, null));

        // Then
        var updated = await ApiClient.ReadAsync<RoleDto>(response);
        Assert.Equal(everyone with { Permissions = readOnly }, updated);
        var bobsView = await bob.GetAsync<ServerDetailDto>(ApiRoutes.Servers.Item(server.Id));
        Assert.Equal(readOnly, bobsView.MyPermissions);
    }

    [Theory]
    [Trait("Story", "API-ROLE-02")]
    [InlineData("create")]
    [InlineData("edit")]
    [InlineData("delete")]
    public async Task A_regular_member_cant_manage_roles(string action)
    {
        // Given Bob joined through an invite, so he only has @everyone
        using var owner = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var role = await owner.CreateRoleAsync(server.Id, "Mods", Blue, Permissions.ManageMessages);
        await bob.JoinAsync(owner, server.Id);

        // When
        var response = await DoAsync(bob, action, server.Id, role.Id);

        // Then
        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
        // (control: the owner can do exactly the same thing)
        var control = await DoAsync(owner, action, server.Id, role.Id);
        await ApiClient.AssertStatusAsync(control, action switch
        {
            "create" => HttpStatusCode.Created,
            "edit" => HttpStatusCode.OK,
            _ => HttpStatusCode.NoContent,
        });
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public async Task Someone_outside_the_server_cant_create_a_role_there()
    {
        // Given
        using var owner = await NewUserAsync();
        using var outsider = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        // (control: the owner can create one, so the 404 below isn't just a missing route)
        await owner.CreateRoleAsync(server.Id);

        // When
        var response = await outsider.PostAsync(ApiRoutes.Servers.Roles(server.Id),
            new CreateRoleRequest("Intruders", null, Permissions.Administrator));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Theory]
    [Trait("Story", "API-ROLE-02")]
    [InlineData("edit")]
    [InlineData("delete")]
    public async Task A_role_that_doesnt_exist_isnt_found(string action)
    {
        // Given
        using var owner = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var real = await owner.CreateRoleAsync(server.Id);
        // (control: editing a real role works, so the 404 below isn't just a missing route)
        await owner.PatchAsync<RoleDto>(ApiRoutes.Servers.Role(server.Id, real.Id), new UpdateRoleRequest("Renamed", null, null, null));

        // When
        var response = await DoAsync(owner, action, server.Id, Guid.NewGuid());

        // Then
        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> DoAsync(ApiClient who, string action, Guid serverId, Guid roleId) => action switch
    {
        "create" => who.PostAsync(ApiRoutes.Servers.Roles(serverId), new CreateRoleRequest(Unique.Name("role"), null, Permissions.None)),
        "edit" => who.PatchAsync(ApiRoutes.Servers.Role(serverId, roleId), new UpdateRoleRequest(Unique.Name("renamed"), null, null, null)),
        "delete" => who.DeleteAsync(ApiRoutes.Servers.Role(serverId, roleId)),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };
}
