using Application.Common;
using Contracts;

namespace Application.Auth;

/// <summary>The auth failures with a contract <c>code</c>.</summary>
public static class AuthErrors
{
    public static readonly AppError UserNameTaken = AppError.Conflict(ErrorCodes.UserNameTaken, "That username is already taken.");
    public static readonly AppError EmailTaken = AppError.Conflict(ErrorCodes.EmailTaken, "That email is already in use.");

    /// <summary>
    /// Used for a wrong password and an unknown user alike, so the answer never reveals which accounts exist.
    /// </summary>
    public static readonly AppError InvalidCredentials =
        new(ErrorKind.Unauthorized, ErrorCodes.InvalidCredentials, "The username, email or password is incorrect.");

    /// <summary>Used for every unusable refresh token (unknown, expired, revoked, already rotated) alike.</summary>
    public static readonly AppError InvalidRefreshToken =
        new(ErrorKind.Unauthorized, ErrorCodes.InvalidRefreshToken, "The refresh token is invalid or has expired.");
}
