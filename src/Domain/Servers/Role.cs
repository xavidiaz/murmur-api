namespace Domain.Servers;

/// <summary>A named set of permissions within a server. Every server has exactly one <c>@everyone</c> role at position 0.</summary>
public sealed class Role
{
    public const int NameMaxLength = 100;
    public const string EveryoneName = "@everyone";
    public const Permissions EveryoneDefaultPermissions =
        Permissions.ViewChannel | Permissions.SendMessages | Permissions.CreateInvite;

    private Role() { } // EF Core

    public Guid Id { get; private set; }
    public Guid ServerId { get; private set; }
    public string Name { get; private set; } = "";
    public int? Color { get; private set; }
    public int Position { get; private set; }
    public Permissions Permissions { get; private set; }
    public bool IsEveryone { get; private set; }

    internal static Role Create(Guid serverId, string name, Permissions permissions, int? color, int position, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
            throw new ArgumentException($"A role name must be 1–{NameMaxLength} characters once trimmed.", nameof(name));
        return new Role
        {
            Id = Guid.CreateVersion7(now),
            ServerId = serverId,
            Name = name.Trim(),
            Color = color,
            Position = position,
            Permissions = permissions,
        };
    }

    internal static Role CreateEveryone(Guid serverId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        ServerId = serverId,
        Name = EveryoneName,
        Position = 0,
        Permissions = EveryoneDefaultPermissions,
        IsEveryone = true,
    };
}
