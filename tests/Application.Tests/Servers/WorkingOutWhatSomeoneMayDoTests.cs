using Application.Servers;
using Domain.Servers;
using Xunit;

namespace Application.Tests.Servers;

/// <summary>Working out what someone may do — API-ROLE-01: <see cref="PermissionService"/>.</summary>
public sealed class WorkingOutWhatSomeoneMayDoTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Permissions EveryoneDefaults = Role.EveryoneDefaultPermissions;

    private readonly Guid _owner = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    private Server ServerWithMember(out Role moderator, out Role channelManager)
    {
        var server = Server.Create(_owner, "perms", null, Now);
        server.AddMember(_member, Now);
        moderator = server.CreateRole("moderator", Permissions.ManageMessages, null, Now);
        channelManager = server.CreateRole("channels", Permissions.ManageChannels | Permissions.ViewChannel, null, Now);
        return server;
    }

    // ---- A member can do what @everyone allows plus what each of their roles allows. ----

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_member_without_roles_can_do_what_everyone_allows()
    {
        // Given
        var server = ServerWithMember(out _, out _);

        // When
        var permissions = PermissionService.EffectivePermissions(server, _member);

        // Then
        Assert.Equal(EveryoneDefaults, permissions);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_member_with_one_role_can_also_do_what_it_allows()
    {
        // Given
        var server = ServerWithMember(out _, out var channelManager);
        server.AssignRole(_member, channelManager.Id);

        // When
        var permissions = PermissionService.EffectivePermissions(server, _member);

        // Then: ManageChannels is something @everyone lacks
        Assert.Equal(EveryoneDefaults | Permissions.ManageChannels, permissions);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_member_with_two_roles_can_do_what_either_allows()
    {
        // Given
        var server = ServerWithMember(out var moderator, out var channelManager);
        server.AssignRole(_member, moderator.Id);
        server.AssignRole(_member, channelManager.Id);

        // When
        var permissions = PermissionService.EffectivePermissions(server, _member);

        // Then
        Assert.Equal(EveryoneDefaults | Permissions.ManageMessages | Permissions.ManageChannels, permissions);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void Roles_held_by_other_members_dont_count()
    {
        // Given
        var server = ServerWithMember(out var moderator, out _);
        var other = Guid.CreateVersion7();
        server.AddMember(other, Now);
        server.AssignRole(other, moderator.Id);

        // When
        var permissions = PermissionService.EffectivePermissions(server, _member);

        // Then
        Assert.Equal(EveryoneDefaults, permissions);
    }

    [Theory]
    [Trait("Story", "API-ROLE-01")]
    [InlineData(Permissions.None)]
    [InlineData(Permissions.ViewChannel)]
    public void The_permissions_are_the_union_of_everyone_and_the_members_roles(Permissions everyone)
    {
        var permissions = PermissionService.Compute(_owner, _member, isMember: true, everyone,
            [Permissions.SendMessages, Permissions.CreateInvite | Permissions.SendMessages]);

        Assert.Equal(everyone | Permissions.SendMessages | Permissions.CreateInvite, permissions);
    }

    // ---- An administrator role, or being the owner, allows everything. ----

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_member_with_an_administrator_role_can_do_everything()
    {
        // Given
        var server = ServerWithMember(out _, out _);
        var admin = server.CreateRole("admin", Permissions.Administrator, null, Now);
        server.AssignRole(_member, admin.Id);

        // When
        var permissions = PermissionService.EffectivePermissions(server, _member);

        // Then
        Assert.Equal(PermissionService.All, permissions);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void If_everyone_is_an_administrator_every_member_can_do_everything()
    {
        var permissions = PermissionService.Compute(_owner, _member, isMember: true, Permissions.Administrator, []);

        Assert.Equal(PermissionService.All, permissions);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void The_owner_can_do_everything_without_any_role()
    {
        var server = ServerWithMember(out _, out _);

        Assert.Equal(PermissionService.All, PermissionService.EffectivePermissions(server, _owner));
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void The_owner_can_do_everything_even_if_everyone_is_stripped_to_nothing()
    {
        var permissions = PermissionService.Compute(_owner, _owner, isMember: true, Permissions.None, []);

        Assert.Equal(PermissionService.All, permissions);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void All_means_every_flag_in_the_contract()
    {
        var everyFlag = Enum.GetValues<Contracts.Servers.Permissions>()
            .Aggregate(Contracts.Servers.Permissions.None, (all, p) => all | p);

        Assert.Equal((long)everyFlag, (long)PermissionService.All);
    }

    // ---- Someone who isn't a member can do nothing. ----

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void Someone_who_isnt_a_member_can_do_nothing()
    {
        var server = ServerWithMember(out _, out _);

        Assert.Equal(Permissions.None, PermissionService.EffectivePermissions(server, Guid.CreateVersion7()));
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void Someone_who_isnt_a_member_can_do_nothing_even_if_everyone_is_an_administrator()
    {
        var permissions = PermissionService.Compute(_owner, _member, isMember: false, Permissions.Administrator, [Permissions.ManageServer]);

        Assert.Equal(Permissions.None, permissions);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_members_access_read_model_counts_their_roles_too()
    {
        var access = new ServerAccess(Guid.CreateVersion7(), "perms", null, _owner, EveryoneDefaults,
            MemberRolePermissions: [Permissions.ManageChannels], MemberCount: 2);

        Assert.Equal(EveryoneDefaults | Permissions.ManageChannels, PermissionService.EffectivePermissions(access, _member));
    }
}
