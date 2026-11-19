using Domain.Invites;

namespace Application.Invites;

/// <summary>Port: persistence for invites.</summary>
public interface IInviteRepository
{
    /// <summary>
    /// Saves a new invite. Returns false, saving nothing, when its code is already taken, so the caller can retry
    /// with a fresh code.
    /// </summary>
    Task<bool> TryAddAsync(Invite invite, CancellationToken ct);
}
