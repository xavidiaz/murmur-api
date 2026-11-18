namespace Murmur.Contracts.Realtime;

/// Client → server hub methods. Clients call them by member name.
public interface IChatHub
{
    Task SubscribeChannel(Guid channelId);             // requires ViewChannel; joins channel:{id}; HubException otherwise
    Task UnsubscribeChannel(Guid channelId);
    Task TypingInChannel(Guid channelId);              // requires SendMessages + subscribed-able
    Task TypingInDm(Guid conversationId);              // requires participant
    Task Heartbeat();                                  // no-op keep-alive; returns immediately
    Task<IReadOnlyList<Guid>> GetOnlineUsers();        // online user ids sharing a server or DM with the caller (excluding caller)
}
