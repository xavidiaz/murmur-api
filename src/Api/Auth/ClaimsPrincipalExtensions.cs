using System.Security.Claims;

namespace Api.Auth;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id from the <c>sub</c> claim, or null when missing/malformed.</summary>
    public static Guid? UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;
}
