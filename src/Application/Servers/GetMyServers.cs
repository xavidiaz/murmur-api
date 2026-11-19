using Contracts.Servers;

namespace Application.Servers;

/// <summary>The servers I'm a member of, in creation order (API-SRV-03).</summary>
public sealed class GetMyServers(IServerRepository servers)
{
    public async Task<IReadOnlyList<ServerDto>> HandleAsync(Guid userId, CancellationToken ct) =>
        [.. (await servers.ListForMemberAsync(userId, ct)).Select(s => new ServerDto(s.Id, s.Name, s.IconUrl, s.OwnerId))];
}
