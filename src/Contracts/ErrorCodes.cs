namespace Contracts;

public static class ErrorCodes
{
    public const string UserNameTaken    = "username_taken";     // 409
    public const string EmailTaken       = "email_taken";        // 409
    public const string InvalidCredentials = "invalid_credentials"; // 401 (login)
    public const string InvalidRefreshToken = "invalid_refresh_token"; // 401 (refresh)
    public const string InviteExpired    = "invite_expired";     // 410
    public const string InviteExhausted  = "invite_exhausted";   // 410
    public const string OwnerCannotLeave = "owner_cannot_leave"; // 409
    public const string RoleHierarchy    = "role_hierarchy";     // 403
    public const string EveryoneRoleImmutable = "everyone_role_immutable"; // 400
    public const string NoSharedServer   = "no_shared_server";   // 403 (open DM)
}
