using System.Text.Json.Serialization;
using Contracts.Servers;

namespace Contracts.Realtime;

[JsonConverter(typeof(JsonStringEnumConverter<PresenceStatus>))]
public enum PresenceStatus { Offline, Online }

public sealed record MessageDeletedEvent(Guid MessageId, Guid? ChannelId, Guid? ConversationId);
public sealed record ChannelDeletedEvent(Guid ServerId, Guid ChannelId);
public sealed record ServerDeletedEvent(Guid ServerId);
public sealed record MemberJoinedEvent(Guid ServerId, MemberDto Member);
public sealed record MemberLeftEvent(Guid ServerId, Guid UserId, bool Kicked);
public sealed record PresenceChangedEvent(Guid UserId, PresenceStatus Status);
public sealed record TypingEvent(Guid UserId, string DisplayName, Guid? ChannelId, Guid? ConversationId);
