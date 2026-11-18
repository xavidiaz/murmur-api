using Contracts.Users;

namespace Application.Auth;

/// <summary>"Who am I?": the signed-in user's profile, or null when the account no longer exists.</summary>
public sealed class GetCurrentUser(IUserAccounts accounts)
{
    public async Task<UserDto?> HandleAsync(Guid userId, CancellationToken ct) =>
        (await accounts.FindByIdAsync(userId, ct))?.ToDto();
}
