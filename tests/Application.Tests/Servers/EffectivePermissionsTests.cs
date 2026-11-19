using Application.Servers;
using Xunit;
using DomainPermissions = Domain.Servers.Permissions;
using DomainServer = Domain.Servers.Server;

namespace Application.Tests.Servers;

/// <summary>Create a server — API-SRV-02: the minimal effective-permissions rule (owner → everything).</summary>
public sealed class EffectivePermissionsTests
{
    private static readonly DomainPermissions All =
        Enum.GetValues<DomainPermissions>().Aggregate(DomainPermissions.None, (all, p) => all | p);

    [Fact]
    [Trait("Story", "API-SRV-02")]
    public void The_owner_has_every_permission()
    {
        // Given
        var owner = Guid.CreateVersion7();
        var server = DomainServer.Create(owner, "My Place", null, DateTimeOffset.UtcNow);

        // When
        var permissions = PermissionService.EffectivePermissions(server, owner);

        // Then
        Assert.Equal(All, permissions);
    }

    [Fact]
    [Trait("Story", "API-SRV-02")]
    public void Someone_who_is_not_a_member_has_no_permissions()
    {
        // Given
        var server = DomainServer.Create(Guid.CreateVersion7(), "My Place", null, DateTimeOffset.UtcNow);

        // When
        var permissions = PermissionService.EffectivePermissions(server, Guid.CreateVersion7());

        // Then
        Assert.Equal(DomainPermissions.None, permissions);
    }
}
