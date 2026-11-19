using Domain.Servers;

namespace Application.Servers;

/// <summary>Port: persistence for the <see cref="Server"/> aggregate.</summary>
public interface IServerRepository
{
    /// <summary>Adds a new server (with its members, roles and channels) and saves it.</summary>
    Task AddAsync(Server server, CancellationToken ct);
}
