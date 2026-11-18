using System.Text.Json.Serialization;

namespace Murmur.Contracts.Channels;

[JsonConverter(typeof(JsonStringEnumConverter<ChannelType>))]
public enum ChannelType { Text }

public sealed record ChannelDto(Guid Id, Guid ServerId, string Name, string? Topic, int Position, ChannelType Type);
public sealed record CreateChannelRequest(string Name, string? Topic);
public sealed record UpdateChannelRequest(string? Name, string? Topic);   // null = unchanged
public sealed record ReorderChannelsRequest(IReadOnlyList<Guid> OrderedChannelIds);
