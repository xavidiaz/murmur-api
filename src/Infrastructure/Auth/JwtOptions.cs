using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

/// <summary>Bound from the <c>Jwt</c> configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>Any string of at least 32 UTF-8 bytes; its UTF-8 bytes are the HMAC key.</summary>
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "murmur";
    public string Audience { get; set; } = "murmur";

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
