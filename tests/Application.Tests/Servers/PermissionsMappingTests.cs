using Xunit;
using ContractPermissions = Contracts.Servers.Permissions;
using DomainPermissions = Domain.Servers.Permissions;

namespace Application.Tests.Servers;

/// <summary>A new server starts in a sensible state — API-SRV-01: the domain's permission flags match the wire contract.</summary>
public sealed class PermissionsMappingTests
{
    [Fact]
    [Trait("Story", "API-SRV-01")]
    public void Domain_and_contract_permissions_have_the_same_names_and_values()
    {
        // Given
        static IEnumerable<(string, long)> Flags<T>() where T : struct, Enum =>
            Enum.GetValues<T>().Select(v => (v.ToString(), Convert.ToInt64(v)));

        // When / Then
        Assert.Equal(Flags<ContractPermissions>(), Flags<DomainPermissions>());
    }
}
