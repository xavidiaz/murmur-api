using Contracts.Channels;
using Contracts.Dms;
using Contracts.Messages;
using Contracts.Servers;

namespace Contracts.Realtime;

/// Server → client. Method names on the wire = member names.
public interface IChatClient
{
    Task MessageCreated(MessageDto message);           // channel:{id} or dm:{id}
    Task MessageUpdated(MessageDto message);
    Task MessageDeleted(MessageDeletedEvent e);

    Task ChannelCreated(ChannelDto channel);           // server:{id}
    Task ChannelUpdated(ChannelDto channel);           // also sent once per moved channel on reorder
    Task ChannelDeleted(ChannelDeletedEvent e);

    Task ServerUpdated(ServerDto server);              // server:{id}
    Task ServerDeleted(ServerDeletedEvent e);

    Task MemberJoined(MemberJoinedEvent e);            // server:{id}
    Task MemberLeft(MemberLeftEvent e);                // server:{id} and the removed user's user:{id}

    Task PresenceChanged(PresenceChangedEvent e);      // user:{id} of everyone sharing a server or DM
    Task TypingStarted(TypingEvent e);                 // channel:{id} or dm:{id}, excluding the typist's connections
    Task DmCreated(DmConversationDto conversation);    // user:{otherUserId}
}
