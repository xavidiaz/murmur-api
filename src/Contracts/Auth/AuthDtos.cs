using Contracts.Users;

namespace Contracts.Auth;

public sealed record RegisterRequest(string UserName, string Email, string Password, string? DisplayName);
public sealed record LoginRequest(string UserNameOrEmail, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);
