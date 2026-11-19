namespace Application.Auth;

/// <summary>An account as the application sees it (no credentials).</summary>
public sealed record UserAccount(Guid Id, string UserName, string DisplayName, string? AvatarUrl);

/// <summary>What to create: <see cref="UserName"/> is already normalized and <see cref="DisplayName"/> resolved.</summary>
public sealed record NewUserAccount(string UserName, string Email, string DisplayName);

/// <summary>Why the account store refused to create an account.</summary>
public enum AccountProblem
{
    DuplicateUserName,
    DuplicateEmail,
    InvalidUserName,
    InvalidEmail,
    InvalidPassword,
}

public sealed record CreateAccountResult(UserAccount? Account, IReadOnlyList<AccountProblem> Problems)
{
    public static CreateAccountResult Created(UserAccount account) => new(account, []);
    public static CreateAccountResult Failed(IReadOnlyList<AccountProblem> problems) => new(null, problems);
}

/// <summary>Port: user accounts and their credentials (implemented with ASP.NET Identity).</summary>
public interface IUserAccounts
{
    Task<CreateAccountResult> CreateAsync(NewUserAccount account, string password, CancellationToken ct);
    Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// The account whose username or email (case-insensitively) is <paramref name="userNameOrEmail"/> and whose
    /// password is <paramref name="password"/>; null otherwise. Takes comparable time whether or not the account exists.
    /// </summary>
    Task<UserAccount?> VerifyCredentialsAsync(string userNameOrEmail, string password, CancellationToken ct);
}

/// <summary>An access token plus a refresh token that starts a new session (token family).</summary>
public sealed record IssuedTokens(
    string AccessToken, DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>A signed JWT access token.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Why a refresh token can't be exchanged.</summary>
public enum RefreshTokenProblem
{
    /// <summary>No such token.</summary>
    Unknown,
    Expired,
    /// <summary>Revoked without being replaced (e.g. signed out, or its session was ended).</summary>
    Revoked,
    /// <summary>
    /// Already exchanged for a newer token, or lost a race to do so. Presenting it again is the reuse signal
    /// that API-AUTH-06 answers by ending the whole session (token family).
    /// </summary>
    AlreadyRotated,
}

/// <summary>The replacement refresh token, in the same session (token family) as the one it replaced.</summary>
public sealed record RotatedRefreshToken(Guid UserId, string RefreshToken, DateTimeOffset ExpiresAt);

public sealed record RotationResult(RotatedRefreshToken? Token, RefreshTokenProblem? Problem)
{
    public static RotationResult Rotated(RotatedRefreshToken token) => new(token, null);
    public static RotationResult Refused(RefreshTokenProblem problem) => new(null, problem);
}

/// <summary>Port: issues JWT access tokens and stores (hashed) refresh tokens.</summary>
public interface ITokenIssuer
{
    /// <summary>Starts a new session (token family) for <paramref name="user"/>.</summary>
    Task<IssuedTokens> IssueAsync(UserAccount user, CancellationToken ct);

    /// <summary>
    /// Exchanges a live refresh token for a new one in the same family. Race-safe: of concurrent calls with the
    /// same token, at most one succeeds; the others get <see cref="RefreshTokenProblem.AlreadyRotated"/>.
    /// </summary>
    Task<RotationResult> RotateRefreshTokenAsync(string refreshToken, CancellationToken ct);

    /// <summary>A fresh access token carrying <paramref name="user"/>'s current data.</summary>
    AccessToken CreateAccessToken(UserAccount user);
}
