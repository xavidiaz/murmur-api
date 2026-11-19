using Domain.Servers;
using Xunit;

namespace Domain.Tests.Servers;

/// <summary>Join with an invite — API-INV-04: a new membership.</summary>
public sealed class JoinWithAnInviteTests
{
    [Fact]
    [Trait("Story", "API-INV-04")]
    public void Joining_makes_a_plain_membership_stamped_with_the_given_time()
    {
        // Given
        var serverId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var now = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

        // When
        var member = ServerMember.Join(serverId, userId, now);

        // Then
        Assert.Equal((serverId, userId, now, (string?)null), (member.ServerId, member.UserId, member.JoinedAt, member.Nickname));
    }
}
