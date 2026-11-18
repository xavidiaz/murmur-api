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
    WeakPassword,
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
}

/// <summary>An access token plus a refresh token that starts a new session (token family).</summary>
public sealed record IssuedTokens(
    string AccessToken, DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Port: issues JWT access tokens and stores (hashed) refresh tokens.</summary>
public interface ITokenIssuer
{
    /// <summary>Starts a new session for <paramref name="user"/>.</summary>
    Task<IssuedTokens> IssueAsync(UserAccount user, CancellationToken ct);
}
