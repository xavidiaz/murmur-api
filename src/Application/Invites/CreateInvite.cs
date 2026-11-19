using Application.Common;
using Application.Servers;
using Contracts.Invites;
using Domain.Invites;
using Permissions = Domain.Servers.Permissions;

namespace Application.Invites;

/// <summary>Creates an invite to a server I'm in (API-INV-02).</summary>
public sealed class CreateInvite(IServerRepository servers, IInviteRepository invites, TimeProvider clock)
{
    /// <summary>Codes are 62^8 strong, so even one collision is astronomically rare; this only bounds a broken RNG.</summary>
    public const int MaxCodeAttempts = 5;

    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(24);

    public async Task<Result<InviteDto>> HandleAsync(Guid userId, Guid serverId, CreateInviteRequest request, CancellationToken ct)
    {
        var access = await servers.GetAccessAsync(serverId, userId, ct);
        if (access is null)
            return ServerErrors.NotFound;
        if (!PermissionService.EffectivePermissions(access, userId).HasFlag(Permissions.CreateInvite))
            return ServerErrors.Forbidden;

        var errors = new Dictionary<string, string[]>();
        if (!Invite.IsValidMaxUses(request.MaxUses))
            errors["maxUses"] = [$"Max uses must be {Invite.MaxUsesMin}–{Invite.MaxUsesMax}, or null for unlimited."];
        var lifetime = Lifetime(request.ExpiresInSeconds);
        if (lifetime is { Valid: false })
            errors["expiresInSeconds"] = [$"Expiry must be {Invite.LifetimeMin.TotalSeconds:0}–{Invite.LifetimeMax.TotalSeconds:0} seconds, 0 for never, or null for {DefaultLifetime.TotalHours:0} hours."];
        if (errors.Count > 0)
            return AppError.Validation(errors);

        var now = clock.GetUtcNow();
        var expiresAt = lifetime?.Value is { } span ? now + span : (DateTimeOffset?)null;
        for (var attempt = 1; attempt <= MaxCodeAttempts; attempt++)
        {
            var invite = Invite.Create(serverId, userId, now, expiresAt, request.MaxUses);
            if (await invites.TryAddAsync(invite, ct))
                return invite.ToDto(access.Name, access.IconUrl, access.MemberCount);
        }
        throw new InvalidOperationException($"No unused invite code after {MaxCodeAttempts} attempts.");
    }

    /// <summary>
    /// <c>ExpiresInSeconds</c>: null means the 24 h default, 0 means never (returns null), and anything else must be a
    /// valid lifetime.
    /// </summary>
    private static (bool Valid, TimeSpan Value)? Lifetime(int? expiresInSeconds) => expiresInSeconds switch
    {
        null => (true, DefaultLifetime),
        0 => null,
        var seconds => (Invite.IsValidLifetime(TimeSpan.FromSeconds(seconds.Value)), TimeSpan.FromSeconds(seconds.Value)),
    };
}
