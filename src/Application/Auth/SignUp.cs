using Application.Common;
using Contracts;
using Contracts.Auth;
using Domain.Users;

namespace Application.Auth;

/// <summary>Creates an account and signs the new user in (API-AUTH-01).</summary>
public sealed class SignUp(IUserAccounts accounts, ITokenIssuer tokens)
{
    public async Task<Result<AuthResponse>> HandleAsync(RegisterRequest request, CancellationToken ct)
    {
        var userName = UserProfile.NormalizeUserName(request.UserName);
        var displayName = UserProfile.ResolveDisplayName(request.DisplayName, userName);

        var created = await accounts.CreateAsync(new NewUserAccount(userName, request.Email, displayName), request.Password, ct);
        if (created.Account is not { } account)
            return ToError(created.Problems);

        var issued = await tokens.IssueAsync(account, ct);
        return new AuthResponse(issued.AccessToken, issued.AccessTokenExpiresAt, issued.RefreshToken,
            issued.RefreshTokenExpiresAt, account.ToDto());
    }

    private static AppError ToError(IReadOnlyList<AccountProblem> problems)
    {
        if (problems.Contains(AccountProblem.DuplicateUserName))
            return AppError.Conflict(ErrorCodes.UserNameTaken, "That username is already taken.");
        if (problems.Contains(AccountProblem.DuplicateEmail))
            return AppError.Conflict(ErrorCodes.EmailTaken, "That email is already in use.");

        var errors = new Dictionary<string, string[]>();
        foreach (var problem in problems.Distinct())
        {
            var (field, message) = problem switch
            {
                AccountProblem.InvalidUserName => ("userName", "Username may only contain a-z, 0-9, '_' and '.'."),
                AccountProblem.InvalidEmail => ("email", "Email is not valid."),
                _ => ("password", "Password must be at least 8 characters and contain a digit."),
            };
            errors[field] = [message];
        }
        return AppError.Validation(errors);
    }
}
