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
