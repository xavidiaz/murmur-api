namespace Domain.Servers;

/// <summary>What a role allows. The numeric values match <c>Contracts.Servers.Permissions</c>; map at the Application boundary.</summary>
[Flags]
public enum Permissions : long
{
    None = 0,
    ViewChannel = 0,    // stub: values not assigned yet
    SendMessages = 0,
    ManageMessages = 0,
    ManageChannels = 0,
    ManageServer = 0,
    CreateInvite = 0,
    Administrator = 0,
}
