using Application.Auth;

namespace Application.Tests.Auth;

/// <summary>In-memory <see cref="IUserAccounts"/> that records what the use case asked for.</summary>
internal sealed class FakeUserAccounts : IUserAccounts
{
    private readonly List<UserAccount> _accounts = [];

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
        _accounts.Add(created);
        return Task.FromResult(CreateAccountResult.Created(created));
    }

    public Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_accounts.SingleOrDefault(a => a.Id == id));
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
