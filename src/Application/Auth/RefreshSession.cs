using Application.Common;
using Contracts.Auth;

namespace Application.Auth;

/// <summary>Exchanges a refresh token for a new access token and a new refresh token (API-AUTH-05).</summary>
public sealed class RefreshSession(IUserAccounts accounts, ITokenIssuer tokens)
{
    public async Task<Result<AuthResponse>> HandleAsync(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return AuthErrors.InvalidRefreshToken;

        var rotation = await tokens.RotateRefreshTokenAsync(request.RefreshToken, ct);
        if (rotation.Token is not { } rotated)
        {
            // API-AUTH-06 hooks in here: RefreshTokenProblem.AlreadyRotated means reuse → end the whole family.
            return AuthErrors.InvalidRefreshToken;
        }

        // Reload so the new access token carries the account's current data.
        var account = await accounts.FindByIdAsync(rotated.UserId, ct);
        if (account is null)
            return AuthErrors.InvalidRefreshToken;

        var access = tokens.CreateAccessToken(account);
        return new AuthResponse(access.Token, access.ExpiresAt, rotated.RefreshToken, rotated.ExpiresAt, account.ToDto());
    }
}
