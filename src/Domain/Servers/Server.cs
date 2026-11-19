namespace Domain.Servers;

/// <summary>A server (aggregate root): its members, roles and channels.</summary>
public sealed class Server
{
    public const int NameMinLength = 1;
    public const int NameMaxLength = 100;

    private readonly List<ServerMember> _members = [];
    private readonly List<Role> _roles = [];
    private readonly List<Channel> _channels = [];

    private Server() { } // EF Core

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? IconUrl { get; private set; }
    public Guid OwnerId { get; private set; }

    public IReadOnlyCollection<ServerMember> Members => _members.AsReadOnly();
    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();
    public IReadOnlyCollection<Channel> Channels => _channels.AsReadOnly();

    /// <summary>Server names are stored trimmed.</summary>
    public static string NormalizeName(string name) => name.Trim();

    /// <summary>A normalized server name: 1–100 characters.</summary>
    public static bool IsValidName(string normalizedName) =>
        normalizedName.Length is >= NameMinLength and <= NameMaxLength;

    /// <summary>
    /// A new server in its starting state: <paramref name="ownerId"/> as the only member (joined at
    /// <paramref name="now"/>), an <c>@everyone</c> role with the default permissions, and a <c>general</c> text channel.
    /// All ids are UUIDv7 stamped with <paramref name="now"/>, which comes from the caller's clock.
    /// </summary>
    /// <exception cref="ArgumentException">The name is empty or longer than 100 characters once trimmed.</exception>
    public static Server Create(Guid ownerId, string name, string? iconUrl, DateTimeOffset now)
    {
        var normalized = NormalizeName(name);
        if (!IsValidName(normalized))
            throw new ArgumentException($"A server name must be {NameMinLength}–{NameMaxLength} characters once trimmed.", nameof(name));

        var server = new Server
        {
            Id = Guid.CreateVersion7(now),
            Name = normalized,
            IconUrl = iconUrl,
            OwnerId = ownerId,
        };
        server._members.Add(new ServerMember(server.Id, ownerId, now));
        server._roles.Add(Role.CreateEveryone(server.Id, now));
        server._channels.Add(Channel.CreateGeneral(server.Id, now));
        return server;
    }
}
