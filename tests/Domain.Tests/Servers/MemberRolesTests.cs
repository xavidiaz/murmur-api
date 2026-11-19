using Domain.Servers;
using Xunit;

namespace Domain.Tests.Servers;

/// <summary>Working out what someone may do — API-ROLE-01: the roles a member holds.</summary>
public sealed class MemberRolesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid Member = Guid.CreateVersion7();

    private static (Server Server, Role Moderator) ServerWithMemberAndRole()
    {
        var server = Server.Create(Owner, "roles", null, Now);
        server.AddMember(Member, Now);
        var moderator = server.CreateRole("moderator", Permissions.ManageMessages, color: 0x3498db, Now);
        return (server, moderator);
    }

    private static ServerMember MemberOf(Server server, Guid userId) => server.Members.Single(m => m.UserId == userId);

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_new_role_goes_below_the_existing_ones_and_is_not_everyone()
    {
        // Given / When
        var (server, moderator) = ServerWithMemberAndRole();
        var helper = server.CreateRole("helper", Permissions.None, color: null, Now);

        // Then
        Assert.Equal((server.Id, "moderator", 0x3498db, 1, Permissions.ManageMessages, false),
            (moderator.ServerId, moderator.Name, moderator.Color, moderator.Position, moderator.Permissions, moderator.IsEveryone));
        Assert.Equal(2, helper.Position);
        Assert.Equal(7, helper.Id.Version);
        Assert.Equal(3, server.Roles.Count);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void Adding_a_member_twice_keeps_one_membership()
    {
        var server = Server.Create(Owner, "roles", null, Now);

        var first = server.AddMember(Member, Now);
        var second = server.AddMember(Member, Now.AddHours(1));

        Assert.Same(first, second);
        Assert.Equal(2, server.Members.Count);
        Assert.Equal(Now, first.JoinedAt);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_new_member_holds_no_roles()
    {
        var (server, _) = ServerWithMemberAndRole();

        Assert.Empty(MemberOf(server, Member).RoleIds);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_member_can_be_given_a_role_of_the_server()
    {
        // Given
        var (server, moderator) = ServerWithMemberAndRole();

        // When
        var outcome = server.AssignRole(Member, moderator.Id);

        // Then
        Assert.Equal(RoleAssignmentOutcome.Done, outcome);
        Assert.Equal([moderator.Id], MemberOf(server, Member).RoleIds);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void Giving_a_role_twice_changes_nothing()
    {
        var (server, moderator) = ServerWithMemberAndRole();
        server.AssignRole(Member, moderator.Id);

        var outcome = server.AssignRole(Member, moderator.Id);

        Assert.Equal(RoleAssignmentOutcome.NoChange, outcome);
        Assert.Single(MemberOf(server, Member).RoleIds);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void Everyone_cant_be_given_or_taken_away()
    {
        // Given
        var (server, _) = ServerWithMemberAndRole();
        var everyone = server.Roles.Single(r => r.IsEveryone);

        // When
        var assign = server.AssignRole(Member, everyone.Id);
        var unassign = server.UnassignRole(Member, everyone.Id);

        // Then
        Assert.Equal((RoleAssignmentOutcome.EveryoneRole, RoleAssignmentOutcome.EveryoneRole), (assign, unassign));
        Assert.Empty(MemberOf(server, Member).RoleIds);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_role_of_another_server_cant_be_given()
    {
        // Given
        var (server, _) = ServerWithMemberAndRole();
        var (other, othersRole) = ServerWithMemberAndRole();

        // When
        var outcome = server.AssignRole(Member, othersRole.Id);

        // Then
        Assert.Equal(RoleAssignmentOutcome.UnknownRole, outcome);
        Assert.Empty(MemberOf(server, Member).RoleIds);
        Assert.NotEqual(server.Id, other.Id);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void Someone_who_isnt_a_member_cant_be_given_a_role()
    {
        var (server, moderator) = ServerWithMemberAndRole();

        var outcome = server.AssignRole(Guid.CreateVersion7(), moderator.Id);

        Assert.Equal(RoleAssignmentOutcome.UnknownMember, outcome);
    }

    [Fact]
    [Trait("Story", "API-ROLE-01")]
    public void A_role_can_be_taken_away_again()
    {
        // Given
        var (server, moderator) = ServerWithMemberAndRole();
        server.AssignRole(Member, moderator.Id);

        // When
        var outcome = server.UnassignRole(Member, moderator.Id);
        var again = server.UnassignRole(Member, moderator.Id);

        // Then
        Assert.Equal((RoleAssignmentOutcome.Done, RoleAssignmentOutcome.NoChange), (outcome, again));
        Assert.Empty(MemberOf(server, Member).RoleIds);
    }
}
