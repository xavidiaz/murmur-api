namespace Domain.Servers;

/// <summary>A named set of permissions within a server. Every server has exactly one <c>@everyone</c> role at position 0.</summary>
public sealed class Role
{
    public const int NameMinLength = 1;
    public const int NameMaxLength = 100;
    public const int ColorMax = 0xFFFFFF;
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

    /// <summary>Role names are stored trimmed.</summary>
    public static string NormalizeName(string name) => name.Trim();

    /// <summary>A normalized role name: 1–100 characters.</summary>
    public static bool IsValidName(string normalizedName) =>
        normalizedName.Length is >= NameMinLength and <= NameMaxLength;

    /// <summary>An RGB colour (0x000000–0xFFFFFF), or none.</summary>
    public static bool IsValidColor(int? color) => color is null or (>= 0 and <= ColorMax);

    /// <summary>Only defined permission flags.</summary>
    public static bool IsValidPermissions(Permissions permissions) => (permissions & ~PermissionFlags.All) == 0;

    internal static Role Create(Guid serverId, string name, Permissions permissions, int? color, int position, DateTimeOffset now)
    {
        var role = new Role { Id = Guid.CreateVersion7(now), ServerId = serverId, Position = position };
        role.Rename(name);
        role.ChangeColor(color);
        role.ChangePermissions(permissions);
        return role;
    }

    internal void Rename(string name)
    {
        var normalized = NormalizeName(name);
        if (!IsValidName(normalized))
            throw new ArgumentException($"A role name must be {NameMinLength}–{NameMaxLength} characters once trimmed.", nameof(name));
        Name = normalized;
    }

    internal void ChangeColor(int? color)
    {
        if (!IsValidColor(color))
            throw new ArgumentOutOfRangeException(nameof(color), color, "A colour must be an RGB value.");
        Color = color;
    }

    internal void ChangePermissions(Permissions permissions)
    {
        if (!IsValidPermissions(permissions))
            throw new ArgumentOutOfRangeException(nameof(permissions), permissions, "Unknown permission flags.");
        Permissions = permissions;
    }

    internal void MoveTo(int position) => Position = position;

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
