namespace Contracts.Servers;

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
