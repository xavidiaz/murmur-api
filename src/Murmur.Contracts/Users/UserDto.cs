namespace Murmur.Contracts.Users;

public sealed record UserDto(Guid Id, string UserName, string DisplayName, string? AvatarUrl);
