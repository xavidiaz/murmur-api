using Application.Common;
using Contracts.Servers;

namespace Application.Servers;

/// <summary>Opens one of my servers (API-SRV-03). A server I'm not in looks exactly like one that doesn't exist.</summary>
public sealed class GetServer(IServerRepository servers)
{
    public async Task<Result<ServerDetailDto>> HandleAsync(Guid userId, Guid serverId, CancellationToken ct)
    {
        var server = await servers.FindForMemberAsync(serverId, userId, ct);
        return server is null ? ServerErrors.NotFound : server.ToDetailDto(userId);
    }
}
