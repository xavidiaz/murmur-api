using Contracts.Users;

namespace Application.Auth;

internal static class UserMapping
{
    public static UserDto ToDto(this UserAccount account) =>
        new(account.Id, account.UserName, account.DisplayName, account.AvatarUrl);
}
