using Application.Common;
using Contracts.Auth;

namespace Application.Auth;

/// <summary>Signs a user in with their username or email (API-AUTH-03); each sign-in starts its own session.</summary>
public sealed class SignIn(IUserAccounts accounts, ITokenIssuer tokens)
{
    public async Task<Result<AuthResponse>> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.UserNameOrEmail) || string.IsNullOrEmpty(request.Password))
            return AuthErrors.InvalidCredentials;

        var account = await accounts.VerifyCredentialsAsync(request.UserNameOrEmail.Trim(), request.Password, ct);
        if (account is null)
            return AuthErrors.InvalidCredentials;

        var issued = await tokens.IssueAsync(account, ct);
        return new AuthResponse(issued.AccessToken, issued.AccessTokenExpiresAt, issued.RefreshToken,
            issued.RefreshTokenExpiresAt, account.ToDto());
    }
}
