namespace Api.AcceptanceTests.Roles;

/// <summary>Give members roles — API-ROLE-03. As a manager, I want to give and take away roles.</summary>
public sealed class GiveMembersRolesTests(PostgresFixture db) : AcceptanceTest(db)
{
    private const Permissions EveryoneDefaults =
        Permissions.ViewChannel | Permissions.SendMessages | Permissions.CreateInvite;

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Giving_a_member_a_role_shows_up_on_their_profile_and_giving_it_twice_is_harmless()
    {
        // Given
        using var owner = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        await bob.JoinAsync(owner, server.Id);
        var role = await owner.CreateRoleAsync(server.Id, "Builders", null, Permissions.ManageChannels);

        // When
        var first = await owner.PutAsync(ApiRoutes.Servers.MemberRole(server.Id, bob.Id, role.Id));

        // Then
        await ApiClient.AssertStatusAsync(first, HttpStatusCode.NoContent);
        Assert.Equal([role.Id], (await owner.GetMemberAsync(server.Id, bob.Id)).RoleIds);
        var bobsView = await bob.GetAsync<ServerDetailDto>(ApiRoutes.Servers.Item(server.Id));
        Assert.Equal(EveryoneDefaults | Permissions.ManageChannels, bobsView.MyPermissions);

        // When given it again
        var second = await owner.PutAsync(ApiRoutes.Servers.MemberRole(server.Id, bob.Id, role.Id));

        // Then nothing changes
        await ApiClient.AssertStatusAsync(second, HttpStatusCode.NoContent);
        Assert.Equal([role.Id], (await owner.GetMemberAsync(server.Id, bob.Id)).RoleIds);
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Taking_it_away_removes_it()
    {
        // Given Bob has a role that grants ManageChannels
        using var owner = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        await bob.JoinAsync(owner, server.Id);
        var role = await owner.CreateRoleAsync(server.Id, "Builders", null, Permissions.ManageChannels);
        await owner.AssignRoleAsync(server.Id, bob.Id, role.Id);

        // When
        var response = await owner.DeleteAsync(ApiRoutes.Servers.MemberRole(server.Id, bob.Id, role.Id));

        // Then
        await ApiClient.AssertStatusAsync(response, HttpStatusCode.NoContent);
        Assert.Empty((await owner.GetMemberAsync(server.Id, bob.Id)).RoleIds);
        var bobsView = await bob.GetAsync<ServerDetailDto>(ApiRoutes.Servers.Item(server.Id));
        Assert.Equal(EveryoneDefaults, bobsView.MyPermissions);
    }

    [Theory]
    [Trait("Story", "API-ROLE-03")]
    [InlineData("a role from another server")]
    [InlineData("someone who isnt a member")]
    [InlineData("a role that doesnt exist")]
    public async Task A_role_from_another_server_or_someone_who_isnt_a_member_isnt_found(string what)
    {
        // Given
        using var owner = await NewUserAsync();
        using var bob = await NewUserAsync();
        using var carol = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        var otherServer = await owner.CreateServerAsync();
        await bob.JoinAsync(owner, server.Id);
        var role = await owner.CreateRoleAsync(server.Id);
        var otherRole = await owner.CreateRoleAsync(otherServer.Id);
        // (control: the valid assignment works, so the 404 below isn't just a missing route)
        await owner.AssignRoleAsync(server.Id, bob.Id, role.Id);

        // When
        var url = what switch
        {
            "a role from another server" => ApiRoutes.Servers.MemberRole(server.Id, bob.Id, otherRole.Id),
            "someone who isnt a member" => ApiRoutes.Servers.MemberRole(server.Id, carol.Id, role.Id),
            "a role that doesnt exist" => ApiRoutes.Servers.MemberRole(server.Id, bob.Id, Guid.NewGuid()),
            _ => throw new ArgumentOutOfRangeException(nameof(what), what, null),
        };
        var response = await owner.PutAsync(url);

        // Then
        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task When_a_role_is_deleted_members_who_had_it_lose_it()
    {
        // Given
        using var owner = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        await bob.JoinAsync(owner, server.Id);
        var doomed = await owner.CreateRoleAsync(server.Id, "Doomed");
        var kept = await owner.CreateRoleAsync(server.Id, "Kept");
        await owner.AssignRoleAsync(server.Id, bob.Id, doomed.Id);
        await owner.AssignRoleAsync(server.Id, bob.Id, kept.Id);

        // When
        var response = await owner.DeleteAsync(ApiRoutes.Servers.Role(server.Id, doomed.Id));

        // Then
        await ApiClient.AssertStatusAsync(response, HttpStatusCode.NoContent);
        Assert.Equal([kept.Id], (await owner.GetMemberAsync(server.Id, bob.Id)).RoleIds);
    }

    [Theory]
    [Trait("Story", "API-ROLE-03")]
    [InlineData("himself")]
    [InlineData("another member")]
    public async Task A_regular_member_cant_give_out_roles(string target)
    {
        // Given Bob and Carol joined through invites, so they only have @everyone
        using var owner = await NewUserAsync();
        using var bob = await NewUserAsync();
        using var carol = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        await bob.JoinAsync(owner, server.Id);
        await carol.JoinAsync(owner, server.Id);
        var role = await owner.CreateRoleAsync(server.Id, "Mods", null, Permissions.ManageServer);
        var userId = target == "himself" ? bob.Id : carol.Id;

        // When
        var response = await bob.PutAsync(ApiRoutes.Servers.MemberRole(server.Id, userId, role.Id));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
        Assert.Empty((await owner.GetMemberAsync(server.Id, userId)).RoleIds);
        // (control: the owner can make the same assignment)
        await owner.AssignRoleAsync(server.Id, userId, role.Id);
    }

    [Fact]
    [Trait("Story", "API-ROLE-03")]
    public async Task Everyone_cant_be_given_out()
    {
        // Given
        using var owner = await NewUserAsync();
        using var bob = await NewUserAsync();
        var server = await owner.CreateServerAsync();
        await bob.JoinAsync(owner, server.Id);
        var everyone = Assert.Single(server.Roles);

        // When
        var response = await owner.PutAsync(ApiRoutes.Servers.MemberRole(server.Id, bob.Id, everyone.Id));

        // Then
        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.EveryoneRoleImmutable);
    }
}
