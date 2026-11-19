using Domain.Servers;

namespace Application.Servers;

/// <summary>A server as shown in a list.</summary>
public sealed record ServerSummary(Guid Id, string Name, string? IconUrl, Guid OwnerId);

/// <summary>
/// What a member needs to know to act in a server, read in one query without loading the aggregate:
/// <see cref="MemberRolePermissions"/> are the permissions of the roles this member holds, and
/// <see cref="MemberCount"/> is counted by the database.
/// </summary>
public sealed record ServerAccess(Guid ServerId, string Name, string? IconUrl, Guid OwnerId, Permissions EveryonePermissions,
    IReadOnlyList<Permissions> MemberRolePermissions, int MemberCount);

/// <summary>
/// A member as listed: their profile, nickname, when they joined and the roles they hold (never <c>@everyone</c>), in
/// role position order.
/// </summary>
public sealed record MemberSummary(Contracts.Users.UserDto User, string? Nickname, DateTimeOffset JoinedAt, IReadOnlyList<Guid> RoleIds);

/// <summary>Port: persistence for the <see cref="Server"/> aggregate.</summary>
public interface IServerRepository
{
    /// <summary>Adds a new server (with its members, roles and channels) and saves it.</summary>
    Task AddAsync(Server server, CancellationToken ct);

    /// <summary>The servers <paramref name="userId"/> is a member of, ordered by id (UUIDv7, so creation order).</summary>
    Task<IReadOnlyList<ServerSummary>> ListForMemberAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// The server with its roles and channels, or null when it doesn't exist <b>or</b> <paramref name="userId"/>
    /// isn't a member. Both cases come back the same way, so callers can't tell them apart. Read-only: the result
    /// carries only the caller's own membership (with its role assignments) in <see cref="Server.Members"/> and must
    /// not be saved.
    /// </summary>
    Task<Server?> FindForMemberAsync(Guid serverId, Guid userId, CancellationToken ct);

    /// <summary>
    /// <see cref="ServerAccess"/> for <paramref name="userId"/>, or null when the server doesn't exist <b>or</b> the
    /// user isn't a member (indistinguishable, as with <see cref="FindForMemberAsync"/>).
    /// </summary>
    Task<ServerAccess?> GetAccessAsync(Guid serverId, Guid userId, CancellationToken ct);

    /// <summary>
    /// The <b>whole</b> aggregate (members with their roles, roles, channels), tracked, for a write; null when there's
    /// no such server. Save with <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<Server?> LoadAsync(Guid serverId, CancellationToken ct);

    /// <summary>Saves the changes made to aggregates loaded with <see cref="LoadAsync"/>.</summary>
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>The server's roles ordered by position, in one query; null when it doesn't exist or the user isn't a member.</summary>
    Task<IReadOnlyList<Role>?> ListRolesForMemberAsync(Guid serverId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Every member, ordered by when they joined (then by user id), in one query; null when the server doesn't exist or
    /// <paramref name="userId"/> isn't a member.
    /// </summary>
    Task<IReadOnlyList<MemberSummary>?> ListMembersForMemberAsync(Guid serverId, Guid userId, CancellationToken ct);
}
