namespace Domain.Servers;

/// <summary>A user's membership of a server.</summary>
public sealed class ServerMember
{
    public const int NicknameMaxLength = 32;

    private ServerMember() { } // EF Core

    internal ServerMember(Guid serverId, Guid userId, DateTimeOffset joinedAt)
    {
        ServerId = serverId;
        UserId = userId;
        JoinedAt = joinedAt;
    }

    /// <summary>A new membership for someone joining a server (e.g. with an invite). <paramref name="joinedAt"/> comes from the caller's clock.</summary>
    public static ServerMember Join(Guid serverId, Guid userId, DateTimeOffset joinedAt) => new(serverId, userId, joinedAt);

    public Guid ServerId { get; private set; }
    public Guid UserId { get; private set; }
    public string? Nickname { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>The roles this member holds, excluding <c>@everyone</c>.</summary>
    public IReadOnlyCollection<Guid> RoleIds => throw new NotImplementedException();
}
