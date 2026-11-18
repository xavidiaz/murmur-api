using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary>An account. <see cref="IdentityUser{TKey}.UserName"/> is stored lowercase.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 32;
    public const int AvatarUrlMaxLength = 2048;

    public required string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}
