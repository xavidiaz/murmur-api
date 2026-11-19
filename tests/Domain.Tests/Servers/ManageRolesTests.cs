using Domain.Servers;
using Xunit;

namespace Domain.Tests.Servers;

/// <summary>Manage roles — API-ROLE-02: the role rules the Server aggregate enforces.</summary>
public sealed class ManageRolesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.CreateVersion7();

    private static Server ServerWithRoles(out Role a, out Role b, out Role c)
    {
        var server = Server.Create(Owner, "roles", null, Now);
        a = server.CreateRole("a", Permissions.None, null, Now);
        b = server.CreateRole("b", Permissions.None, null, Now);
        c = server.CreateRole("c", Permissions.None, null, Now);
        return server;
    }

    private static string[] Order(Server server) => [.. server.Roles.OrderBy(r => r.Position).Select(r => r.Name)];

    private static Role Everyone(Server server) => server.Roles.Single(r => r.IsEveryone);

    [Theory]
    [Trait("Story", "API-ROLE-02")]
    [InlineData("Moderators", true)]
    [InlineData("  Moderators  ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void A_role_name_is_trimmed_and_must_not_be_empty(string name, bool valid)
    {
        Assert.Equal(valid, Role.IsValidName(Role.NormalizeName(name)));
        if (valid)
            Assert.Equal("Moderators", Server.Create(Owner, "s", null, Now).CreateRole(name, Permissions.None, null, Now).Name);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void A_role_name_can_be_100_characters_but_not_101()
    {
        Assert.True(Role.IsValidName(new string('r', 100)));
        Assert.False(Role.IsValidName(new string('r', 101)));
        Assert.Throws<ArgumentException>(() =>
            Server.Create(Owner, "s", null, Now).CreateRole(new string('r', 101), Permissions.None, null, Now));
    }

    [Theory]
    [Trait("Story", "API-ROLE-02")]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(0xFFFFFF, true)]
    [InlineData(-1, false)]
    [InlineData(0x1000000, false)]
    public void A_colour_is_an_rgb_value_or_none(int? color, bool valid) => Assert.Equal(valid, Role.IsValidColor(color));

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void Only_known_permission_flags_are_valid()
    {
        Assert.True(Role.IsValidPermissions(Permissions.ManageMessages | Permissions.Administrator));
        Assert.False(Role.IsValidPermissions((Permissions)(1L << 40)));
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void A_role_can_be_renamed_recoloured_and_given_new_permissions_and_null_means_unchanged()
    {
        // Given
        var server = ServerWithRoles(out var a, out _, out _);

        // When
        var renamed = server.UpdateRole(a.Id, name: " Mods ", color: null, permissions: null);
        var recoloured = server.UpdateRole(a.Id, name: null, color: 0x3498DB, permissions: Permissions.ManageMessages);

        // Then
        Assert.Equal((RoleChangeOutcome.Done, RoleChangeOutcome.Done), (renamed, recoloured));
        Assert.Equal(("Mods", (int?)0x3498DB, Permissions.ManageMessages, 1), (a.Name, a.Color, a.Permissions, a.Position));
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void Everyone_cant_be_renamed_but_its_permissions_and_colour_can_change()
    {
        // Given
        var server = ServerWithRoles(out _, out _, out _);
        var everyone = Everyone(server);

        // When
        var rename = server.UpdateRole(everyone.Id, name: "everybody", color: null, permissions: Permissions.ViewChannel);
        var permissions = server.UpdateRole(everyone.Id, name: null, color: 0xFF0000, permissions: Permissions.ViewChannel);

        // Then
        Assert.Equal(RoleChangeOutcome.EveryoneRoleImmutable, rename);
        Assert.Equal(RoleChangeOutcome.Done, permissions);
        Assert.Equal(("@everyone", Permissions.ViewChannel, (int?)0xFF0000), (everyone.Name, everyone.Permissions, everyone.Color));
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void A_refused_change_changes_nothing()
    {
        var server = ServerWithRoles(out _, out _, out _);
        var everyone = Everyone(server);

        server.UpdateRole(everyone.Id, name: "everybody", color: 0x123456, permissions: Permissions.None);

        Assert.Equal((Role.EveryoneDefaultPermissions, (int?)null), (everyone.Permissions, everyone.Color));
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void An_unknown_role_cant_be_changed_or_deleted()
    {
        var server = ServerWithRoles(out _, out _, out _);

        Assert.Equal(RoleChangeOutcome.UnknownRole, server.UpdateRole(Guid.CreateVersion7(), "x", null, null));
        Assert.Equal(RoleChangeOutcome.UnknownRole, server.MoveRole(Guid.CreateVersion7(), 1));
        Assert.Equal(RoleChangeOutcome.UnknownRole, server.DeleteRole(Guid.CreateVersion7()));
    }

    // ---- Position: "move to position p", p in 1..max ----

    [Theory]
    [Trait("Story", "API-ROLE-02")]
    [InlineData("c", 1, new[] { "@everyone", "c", "a", "b" })]
    [InlineData("a", 3, new[] { "@everyone", "b", "c", "a" })]
    [InlineData("a", 2, new[] { "@everyone", "b", "a", "c" })]
    [InlineData("b", 2, new[] { "@everyone", "a", "b", "c" })]
    public void Moving_a_role_puts_it_at_that_position_and_renumbers_the_others(string role, int to, string[] expected)
    {
        // Given
        var server = ServerWithRoles(out _, out _, out _);

        // When
        var outcome = server.MoveRole(server.Roles.Single(r => r.Name == role).Id, to);

        // Then
        Assert.Equal(RoleChangeOutcome.Done, outcome);
        Assert.Equal(expected, Order(server));
        Assert.Equal([0, 1, 2, 3], server.Roles.Select(r => r.Position).Order());
    }

    [Theory]
    [Trait("Story", "API-ROLE-02")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    public void A_position_outside_1_to_the_number_of_roles_is_refused(int to)
    {
        var server = ServerWithRoles(out var a, out _, out _);

        Assert.False(server.IsValidRolePosition(to));
        Assert.Throws<ArgumentOutOfRangeException>(() => server.MoveRole(a.Id, to));
        Assert.Equal(["@everyone", "a", "b", "c"], Order(server));
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void Everyone_doesnt_move()
    {
        var server = ServerWithRoles(out _, out _, out _);

        var outcome = server.MoveRole(Everyone(server).Id, 2);

        Assert.Equal(RoleChangeOutcome.EveryoneRoleImmutable, outcome);
        Assert.Equal(0, Everyone(server).Position);
    }

    // ---- Delete ----

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void Deleting_a_role_closes_the_gap_and_takes_it_from_its_holders()
    {
        // Given
        var server = ServerWithRoles(out var a, out var b, out _);
        var member = Guid.CreateVersion7();
        server.AddMember(member, Now);
        server.AssignRole(member, a.Id);
        server.AssignRole(member, b.Id);

        // When
        var outcome = server.DeleteRole(a.Id);

        // Then
        Assert.Equal(RoleChangeOutcome.Done, outcome);
        Assert.Equal(["@everyone", "b", "c"], Order(server));
        Assert.Equal([0, 1, 2], server.Roles.Select(r => r.Position).Order());
        Assert.Equal([b.Id], server.Members.Single(m => m.UserId == member).RoleIds);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void Everyone_cant_be_deleted()
    {
        var server = ServerWithRoles(out _, out _, out _);

        Assert.Equal(RoleChangeOutcome.EveryoneRoleImmutable, server.DeleteRole(Everyone(server).Id));
        Assert.Equal(4, server.Roles.Count);
    }

    [Fact]
    [Trait("Story", "API-ROLE-02")]
    public void A_new_role_goes_on_top_of_the_existing_ones_even_after_a_delete()
    {
        var server = ServerWithRoles(out var a, out _, out _);
        server.DeleteRole(a.Id);

        var d = server.CreateRole("d", Permissions.None, null, Now);

        Assert.Equal(3, d.Position);
    }
}
