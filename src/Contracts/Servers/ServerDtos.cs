using Contracts.Channels;
using Contracts.Users;

namespace Contracts.Servers;

public sealed record ServerDto(Guid Id, string Name, string? IconUrl, Guid OwnerId);

public sealed record ServerDetailDto(
    Guid Id, string Name, string? IconUrl, Guid OwnerId,
    IReadOnlyList<ChannelDto> Channels,      // ordered by Position
    IReadOnlyList<RoleDto> Roles,            // ordered by Position; @everyone first (Position 0)
    Permissions MyPermissions);              // caller's effective permissions

public sealed record CreateServerRequest(string Name, string? IconUrl);
public sealed record UpdateServerRequest(string? Name, string? IconUrl);   // null = unchanged

public sealed record MemberDto(UserDto User, string? Nickname, DateTimeOffset JoinedAt, IReadOnlyList<Guid> RoleIds);

public sealed record RoleDto(Guid Id, Guid ServerId, string Name, int? Color, int Position, Permissions Permissions, bool IsEveryone);
public sealed record CreateRoleRequest(string Name, int? Color, Permissions Permissions);
public sealed record UpdateRoleRequest(string? Name, int? Color, Permissions? Permissions, int? Position);
