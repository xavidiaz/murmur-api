namespace Domain.Servers;

/// <summary>A user's membership of a server.</summary>
public sealed class ServerMember
{
    private ServerMember() { } // EF Core

    internal ServerMember(Guid serverId, Guid userId, DateTimeOffset joinedAt)
    {
        ServerId = serverId;
        UserId = userId;
        JoinedAt = joinedAt;
    }

    public Guid ServerId { get; private set; }
    public Guid UserId { get; private set; }
    public string? Nickname { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
}
