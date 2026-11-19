namespace Domain.Servers;

/// <summary>A server (aggregate root): its members, roles and channels.</summary>
public sealed class Server
{
    public const int NameMinLength = 1;
    public const int NameMaxLength = 100;
    public const int IconUrlMaxLength = 2048;

    private readonly List<ServerMember> _members = [];
    private readonly List<Role> _roles = [];
    private readonly List<Channel> _channels = [];

    private Server() { } // EF Core

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? IconUrl { get; private set; }
    public Guid OwnerId { get; private set; }

    public IReadOnlyCollection<ServerMember> Members => _members.AsReadOnly();
    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();
    public IReadOnlyCollection<Channel> Channels => _channels.AsReadOnly();

    /// <summary>Adds <paramref name="userId"/> as a member (or returns the existing membership unchanged).</summary>
    public ServerMember AddMember(Guid userId, DateTimeOffset joinedAt)
    {
        if (_members.FirstOrDefault(m => m.UserId == userId) is { } existing)
            return existing;
        var member = ServerMember.Join(Id, userId, joinedAt);
        _members.Add(member);
        return member;
    }

    /// <summary>
    /// A new role below the existing ones. Minimal for now; API-ROLE-02 adds its validation and hierarchy rules.
    /// </summary>
    public Role CreateRole(string name, Permissions permissions, int? color, DateTimeOffset now)
    {
        var role = Role.Create(Id, name, permissions, color, position: _roles.Max(r => r.Position) + 1, now);
        _roles.Add(role);
        return role;
    }

    /// <summary>
    /// Gives a member one of this server's roles. Refused for non-members, for roles that aren't this server's, and for
    /// <c>@everyone</c> (every member has it implicitly).
    /// </summary>
    public RoleAssignmentOutcome AssignRole(Guid userId, Guid roleId) =>
        CheckAssignment(userId, roleId, out var member) ?? (member!.Assign(roleId) ? RoleAssignmentOutcome.Done : RoleAssignmentOutcome.NoChange);

    /// <summary>Takes a role away from a member; the same refusals as <see cref="AssignRole"/>.</summary>
    public RoleAssignmentOutcome UnassignRole(Guid userId, Guid roleId) =>
        CheckAssignment(userId, roleId, out var member) ?? (member!.Unassign(roleId) ? RoleAssignmentOutcome.Done : RoleAssignmentOutcome.NoChange);

    private RoleAssignmentOutcome? CheckAssignment(Guid userId, Guid roleId, out ServerMember? member)
    {
        member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
            return RoleAssignmentOutcome.UnknownMember;
        var role = _roles.FirstOrDefault(r => r.Id == roleId);
        if (role is null)
            return RoleAssignmentOutcome.UnknownRole;
        return role.IsEveryone ? RoleAssignmentOutcome.EveryoneRole : null;
    }

    /// <summary>Server names are stored trimmed.</summary>
    public static string NormalizeName(string name) => name.Trim();

    /// <summary>A normalized server name: 1–100 characters.</summary>
    public static bool IsValidName(string normalizedName) =>
        normalizedName.Length is >= NameMinLength and <= NameMaxLength;

    /// <summary>An icon URL: an absolute http(s) URI of at most 2048 characters.</summary>
    public static bool IsValidIconUrl(string iconUrl) =>
        iconUrl.Length <= IconUrlMaxLength
        && Uri.TryCreate(iconUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    /// <summary>
    /// A new server in its starting state: <paramref name="ownerId"/> as the only member (joined at
    /// <paramref name="now"/>), an <c>@everyone</c> role with the default permissions, and a <c>general</c> text channel.
    /// All ids are UUIDv7 stamped with <paramref name="now"/>, which comes from the caller's clock.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The name is empty or longer than 100 characters once trimmed, or the icon URL is not a valid http(s) URL.
    /// </exception>
    public static Server Create(Guid ownerId, string name, string? iconUrl, DateTimeOffset now)
    {
        var normalized = NormalizeName(name);
        if (!IsValidName(normalized))
            throw new ArgumentException($"A server name must be {NameMinLength}–{NameMaxLength} characters once trimmed.", nameof(name));
        if (iconUrl is not null && !IsValidIconUrl(iconUrl))
            throw new ArgumentException("An icon URL must be an absolute http(s) URL.", nameof(iconUrl));

        var server = new Server
        {
            Id = Guid.CreateVersion7(now),
            Name = normalized,
            IconUrl = iconUrl,
            OwnerId = ownerId,
        };
        server._members.Add(new ServerMember(server.Id, ownerId, now));
        server._roles.Add(Role.CreateEveryone(server.Id, now));
        server._channels.Add(Channel.CreateGeneral(server.Id, now));
        return server;
    }
}
