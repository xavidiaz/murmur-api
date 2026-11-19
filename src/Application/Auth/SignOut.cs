using Contracts.Auth;

namespace Application.Auth;

/// <summary>
/// Signs this device out by ending the session of its refresh token (API-AUTH-07). It always succeeds from the
/// caller's point of view, so it never reveals whether the token existed or belonged to someone else.
/// </summary>
public sealed class SignOut(ITokenIssuer tokens)
{
    public async Task HandleAsync(Guid userId, LogoutRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return;
        await tokens.EndSessionAsync(userId, request.RefreshToken, ct);
    }
}
