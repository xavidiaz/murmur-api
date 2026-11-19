namespace Domain.Servers;

/// <summary>A channel within a server.</summary>
public sealed class Channel
{
    public const int NameMaxLength = 100;
    public const int TopicMaxLength = 1024;
    public const string GeneralName = "general";

    private Channel() { } // EF Core

    public Guid Id { get; private set; }
    public Guid ServerId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Topic { get; private set; }
    public int Position { get; private set; }
    public ChannelType Type { get; private set; }

    internal static Channel CreateGeneral(Guid serverId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        ServerId = serverId,
        Name = GeneralName,
        Position = 0,
        Type = ChannelType.Text,
    };
}
