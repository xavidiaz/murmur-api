using Application.Common;
using Contracts;

namespace Application.Roles;

public static class RoleErrors
{
    public static readonly AppError NotFound = new(ErrorKind.NotFound, null, "Role not found.");

    public static readonly AppError EveryoneRoleImmutable =
        new(ErrorKind.Validation, ErrorCodes.EveryoneRoleImmutable, "@everyone can't be renamed, moved or deleted.");
}
