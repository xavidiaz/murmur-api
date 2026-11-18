using Murmur.Contracts.Users;

namespace Murmur.Contracts.Dms;

public sealed record OpenDmRequest(Guid UserId);
public sealed record DmConversationDto(Guid Id, UserDto OtherUser, DateTimeOffset? LastMessageAt);
