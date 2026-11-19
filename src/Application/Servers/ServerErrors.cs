using Application.Common;

namespace Application.Servers;

public static class ServerErrors
{
    /// <summary>
    /// Used both when the server doesn't exist and when the caller isn't a member, so a 404 never reveals
    /// whether a server exists.
    /// </summary>
    public static readonly AppError NotFound = new(ErrorKind.NotFound, null, "Server not found.");
}
