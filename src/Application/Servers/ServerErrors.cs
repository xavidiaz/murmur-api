using Application.Common;

namespace Application.Servers;

public static class ServerErrors
{
    /// <summary>
    /// Used both when the server doesn't exist and when the caller isn't a member, so a 404 never reveals
    /// whether a server exists.
    /// </summary>
    public static readonly AppError NotFound = new(ErrorKind.NotFound, null, "Server not found.");

    /// <summary>The caller is a member but lacks the permission the action needs.</summary>
    public static readonly AppError Forbidden = new(ErrorKind.Forbidden, null, "You don't have permission to do that.");
}
