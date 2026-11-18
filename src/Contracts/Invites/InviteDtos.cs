namespace Contracts.Invites;

public sealed record CreateInviteRequest(int? MaxUses, int? ExpiresInSeconds);   // null = unlimited uses / 24h default

public sealed record InviteDto(
    string Code, Guid ServerId, string ServerName, string? ServerIconUrl,
    Guid CreatorId, DateTimeOffset? ExpiresAt, int? MaxUses, int Uses, int MemberCount);
