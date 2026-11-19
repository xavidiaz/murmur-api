using Application.Common;
using Contracts;

namespace Application.Invites;

public static class InviteErrors
{
    /// <summary>Unknown and malformed codes alike.</summary>
    public static readonly AppError NotFound = new(ErrorKind.NotFound, null, "Invite not found.");

    public static readonly AppError Expired = new(ErrorKind.Gone, ErrorCodes.InviteExpired, "This invite has expired.");

    public static readonly AppError Exhausted = new(ErrorKind.Gone, ErrorCodes.InviteExhausted, "This invite has been used up.");
}
