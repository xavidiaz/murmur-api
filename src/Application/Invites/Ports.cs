using Domain.Invites;

namespace Application.Invites;

/// <summary>An invite with what a preview shows about its server; <see cref="MemberCount"/> is counted by the database.</summary>
public sealed record InvitePreview(Invite Invite, string ServerName, string? ServerIconUrl, int MemberCount);

/// <summary>Port: persistence for invites.</summary>
public interface IInviteRepository
{
    /// <summary>
    /// Saves a new invite. Returns false, saving nothing, when its code is already taken, so the caller can retry
    /// with a fresh code.
    /// </summary>
    Task<bool> TryAddAsync(Invite invite, CancellationToken ct);

    /// <summary>The invite with its server's name, icon and member count, in one query; null when there's no such code.</summary>
    Task<InvitePreview?> FindPreviewAsync(string code, CancellationToken ct);
}
