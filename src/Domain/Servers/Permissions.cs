namespace Domain.Servers;

/// <summary>
/// What a role allows. The numeric values match <c>Contracts.Servers.Permissions</c> exactly (enforced by a test);
/// map between the two at the Application boundary.
/// </summary>
[Flags]
public enum Permissions : long
{
    None           = 0,
    ViewChannel    = 1 << 0,
    SendMessages   = 1 << 1,
    ManageMessages = 1 << 2,
    ManageChannels = 1 << 3,
    ManageServer   = 1 << 4,
    CreateInvite   = 1 << 5,
    Administrator  = 1 << 6,
}

public static class PermissionFlags
{
    /// <summary>Every defined flag.</summary>
    public static readonly Permissions All =
        Enum.GetValues<Permissions>().Aggregate(Permissions.None, (all, p) => all | p);
}
