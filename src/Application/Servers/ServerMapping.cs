using Domain.Servers;
using ChannelDto = Contracts.Channels.ChannelDto;
using ContractChannelType = Contracts.Channels.ChannelType;
using ContractPermissions = Contracts.Servers.Permissions;
using RoleDto = Contracts.Servers.RoleDto;
using ServerDetailDto = Contracts.Servers.ServerDetailDto;

namespace Application.Servers;

internal static class ServerMapping
{
    /// <summary>The domain and contract flags have identical values (enforced by a test), so this is a plain cast.</summary>
    public static ContractPermissions ToContract(this Permissions permissions) => (ContractPermissions)(long)permissions;

    public static ContractChannelType ToContract(this ChannelType type) => type switch
    {
        ChannelType.Text => ContractChannelType.Text,
        _ => throw new InvalidOperationException($"Unmapped channel type '{type}'."),
    };

    public static ChannelDto ToDto(this Channel channel) =>
        new(channel.Id, channel.ServerId, channel.Name, channel.Topic, channel.Position, channel.Type.ToContract());

    public static RoleDto ToDto(this Role role) =>
        new(role.Id, role.ServerId, role.Name, role.Color, role.Position, role.Permissions.ToContract(), role.IsEveryone);

    /// <summary>The server as <paramref name="viewerId"/> sees it: channels and roles ordered by position.</summary>
    public static ServerDetailDto ToDetailDto(this Server server, Guid viewerId) =>
        new(server.Id, server.Name, server.IconUrl, server.OwnerId,
            [.. server.Channels.OrderBy(c => c.Position).Select(c => c.ToDto())],
            [.. server.Roles.OrderBy(r => r.Position).Select(r => r.ToDto())],
            PermissionService.EffectivePermissions(server, viewerId).ToContract());
}
