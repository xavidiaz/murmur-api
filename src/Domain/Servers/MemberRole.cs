namespace Domain.Servers;

/// <summary>A role held by a member (never <c>@everyone</c>, which every member has implicitly).</summary>
public sealed class MemberRole
{
    private MemberRole() { } // EF Core

    internal MemberRole(Guid serverId, Guid userId, Guid roleId)
    {
        ServerId = serverId;
        UserId = userId;
        RoleId = roleId;
    }

    public Guid ServerId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
}
