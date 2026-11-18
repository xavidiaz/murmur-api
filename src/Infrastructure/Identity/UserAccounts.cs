using System.Security.Cryptography;
using Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary><see cref="IUserAccounts"/> on top of ASP.NET Identity's <see cref="UserManager{TUser}"/>.</summary>
public sealed class UserAccounts(UserManager<ApplicationUser> users, TimeProvider clock) : IUserAccounts
{
    public async Task<CreateAccountResult> CreateAsync(NewUserAccount account, string password, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(clock.GetUtcNow()),
            UserName = account.UserName,
            Email = account.Email,
            DisplayName = account.DisplayName,
        };

        var result = await users.CreateAsync(user, password);
        return result.Succeeded
            ? CreateAccountResult.Created(ToAccount(user))
            : CreateAccountResult.Failed([.. result.Errors.Select(ToProblem).Distinct()]);
    }

    public async Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString());
        return user is null ? null : ToAccount(user);
    }

    public async Task<UserAccount?> VerifyCredentialsAsync(string userNameOrEmail, string password, CancellationToken ct)
    {
        // Usernames can't contain '@', so the input says which lookup applies. Both use Identity's normalized
        // (upper-cased) columns, so matching ignores capitals.
        var user = userNameOrEmail.Contains('@')
            ? await users.FindByEmailAsync(userNameOrEmail)
            : await users.FindByNameAsync(userNameOrEmail);

        if (user is null)
        {
            // Verify against a throwaway hash so an unknown user costs about as much as a wrong password.
            users.PasswordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash(users.PasswordHasher), password);
            return null;
        }

        return await users.CheckPasswordAsync(user, password) ? ToAccount(user) : null;
    }

    private static readonly ApplicationUser DummyUser = new() { DisplayName = "" };
    private static string? _dummyPasswordHash;

    private static string DummyPasswordHash(IPasswordHasher<ApplicationUser> hasher) =>
        LazyInitializer.EnsureInitialized(ref _dummyPasswordHash,
            () => hasher.HashPassword(DummyUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

    private static UserAccount ToAccount(ApplicationUser user) =>
        new(user.Id, user.UserName!, user.DisplayName, user.AvatarUrl);

    private static AccountProblem ToProblem(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.DuplicateUserName) => AccountProblem.DuplicateUserName,
        nameof(IdentityErrorDescriber.DuplicateEmail) => AccountProblem.DuplicateEmail,
        nameof(IdentityErrorDescriber.InvalidUserName) => AccountProblem.InvalidUserName,
        nameof(IdentityErrorDescriber.InvalidEmail) => AccountProblem.InvalidEmail,
        // PasswordTooShort, PasswordRequiresDigit, … (the password validator's codes)
        _ when error.Code.StartsWith("Password", StringComparison.Ordinal) => AccountProblem.InvalidPassword,
        _ => throw new InvalidOperationException($"Unexpected Identity error '{error.Code}' while creating an account."),
    };
}
