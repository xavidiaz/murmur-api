namespace Domain.Servers;

/// <summary>A server (aggregate root): its members, roles and channels.</summary>
public sealed class Server
{
    private Server() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? IconUrl { get; private set; }
    public Guid OwnerId { get; private set; }
    public IReadOnlyCollection<ServerMember> Members => throw new NotImplementedException();
    public IReadOnlyCollection<Role> Roles => throw new NotImplementedException();
    public IReadOnlyCollection<Channel> Channels => throw new NotImplementedException();

    public static string NormalizeName(string name) => throw new NotImplementedException();
    public static bool IsValidName(string normalizedName) => throw new NotImplementedException();

    public static Server Create(Guid ownerId, string name, string? iconUrl, DateTimeOffset now) =>
        throw new NotImplementedException();
}

public sealed class ServerMember
{
    private ServerMember() { }

    public Guid ServerId { get; private set; }
    public Guid UserId { get; private set; }
    public string? Nickname { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
}

public sealed class Role
{
    private Role() { }

    public Guid Id { get; private set; }
    public Guid ServerId { get; private set; }
    public string Name { get; private set; } = "";
    public int? Color { get; private set; }
    public int Position { get; private set; }
    public Permissions Permissions { get; private set; }
    public bool IsEveryone { get; private set; }
}

public sealed class Channel
{
    private Channel() { }

    public Guid Id { get; private set; }
    public Guid ServerId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Topic { get; private set; }
    public int Position { get; private set; }
    public ChannelType Type { get; private set; }
}
