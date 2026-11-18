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

    private static UserAccount ToAccount(ApplicationUser user) =>
        new(user.Id, user.UserName!, user.DisplayName, user.AvatarUrl);

    private static AccountProblem ToProblem(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.DuplicateUserName) => AccountProblem.DuplicateUserName,
        nameof(IdentityErrorDescriber.DuplicateEmail) => AccountProblem.DuplicateEmail,
        nameof(IdentityErrorDescriber.InvalidUserName) => AccountProblem.InvalidUserName,
        nameof(IdentityErrorDescriber.InvalidEmail) => AccountProblem.InvalidEmail,
        _ => AccountProblem.WeakPassword,
    };
}
