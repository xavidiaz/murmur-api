using Application.Auth;

namespace Application.Tests.Auth;

/// <summary>In-memory <see cref="IUserAccounts"/> that records what the use case asked for.</summary>
internal sealed class FakeUserAccounts : IUserAccounts
{
    private readonly List<(UserAccount Account, string Email, string Password)> _accounts = [];

    public NewUserAccount? LastCreated { get; private set; }
    public string? LastPassword { get; private set; }

    /// <summary>Problems to report on the next <see cref="CreateAsync"/> instead of creating the account.</summary>
    public List<AccountProblem> NextProblems { get; } = [];

    public Task<CreateAccountResult> CreateAsync(NewUserAccount account, string password, CancellationToken ct)
    {
        LastCreated = account;
        LastPassword = password;
        if (NextProblems.Count > 0)
            return Task.FromResult(CreateAccountResult.Failed([.. NextProblems]));

        var created = new UserAccount(Guid.NewGuid(), account.UserName, account.DisplayName, null);
        _accounts.Add((created, account.Email, password));
        return Task.FromResult(CreateAccountResult.Created(created));
    }

    public Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult<UserAccount?>(_accounts.SingleOrDefault(a => a.Account.Id == id).Account);

    /// <summary>Usernames and emails match case-insensitively, like the real store.</summary>
    public Task<UserAccount?> VerifyCredentialsAsync(string userNameOrEmail, string password, CancellationToken ct)
    {
        var match = _accounts.SingleOrDefault(a =>
            string.Equals(a.Account.UserName, userNameOrEmail, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Email, userNameOrEmail, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<UserAccount?>(match.Account is not null && match.Password == password ? match.Account : null);
    }
}

/// <summary><see cref="ITokenIssuer"/> that hands out predictable tokens.</summary>
internal sealed class FakeTokenIssuer : ITokenIssuer
{
    public static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public List<Guid> IssuedFor { get; } = [];

    public Task<IssuedTokens> IssueAsync(UserAccount user, CancellationToken ct)
    {
        IssuedFor.Add(user.Id);
        return Task.FromResult(new IssuedTokens(
            $"access-{user.Id}", Now.AddMinutes(15), $"refresh-{user.Id}", Now.AddDays(30)));
    }
}
