using Contracts.Users;

namespace Contracts.Messages;

public sealed record MessageDto(
    Guid Id,
    Guid? ChannelId,          // exactly one of ChannelId / ConversationId is set
    Guid? ConversationId,
    UserDto Author,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    Guid? Nonce);             // echoed from SendMessageRequest; null everywhere else

public sealed record SendMessageRequest(string Content, Guid? Nonce);
public sealed record EditMessageRequest(string Content);

public sealed record MessagePage(IReadOnlyList<MessageDto> Items, bool HasMore);   // Items newest first
