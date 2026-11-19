using Role = Domain.Servers.Role;
using DomainPermissions = Domain.Servers.Permissions;

namespace Application.Roles;

internal static class RoleValidation
{
    public static void Name(Dictionary<string, string[]> errors, string? name)
    {
        if (name is null || !Role.IsValidName(Role.NormalizeName(name)))
            errors["name"] = [$"Name must be {Role.NameMinLength}–{Role.NameMaxLength} characters."];
    }

    public static void Color(Dictionary<string, string[]> errors, int? color)
    {
        if (!Role.IsValidColor(color))
            errors["color"] = [$"Colour must be an RGB value from 0 to {Role.ColorMax} (0xFFFFFF)."];
    }

    public static void Permissions(Dictionary<string, string[]> errors, DomainPermissions? permissions)
    {
        if (permissions is { } p && !Role.IsValidPermissions(p))
            errors["permissions"] = ["Permissions contains unknown flags."];
    }
}
