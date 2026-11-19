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

    /// <summary>A new role on top of the existing ones (Position = highest + 1). Name, colour and permissions are checked.</summary>
    /// <exception cref="ArgumentException">An invalid name, colour or permission set (see <see cref="Role"/>'s rules).</exception>
    public Role CreateRole(string name, Permissions permissions, int? color, DateTimeOffset now)
    {
        var role = Role.Create(Id, name, permissions, color, position: _roles.Max(r => r.Position) + 1, now);
        _roles.Add(role);
        return role;
    }

    /// <summary>
    /// Changes a role; null leaves a field unchanged. <c>@everyone</c> can't be renamed (its colour and permissions can
    /// change). Refusals change nothing.
    /// </summary>
    /// <exception cref="ArgumentException">An invalid name, colour or permission set.</exception>
    public RoleChangeOutcome UpdateRole(Guid roleId, string? name, int? color, Permissions? permissions)
    {
        var role = _roles.FirstOrDefault(r => r.Id == roleId);
        if (role is null)
            return RoleChangeOutcome.UnknownRole;
        if (role.IsEveryone && name is not null)
            return RoleChangeOutcome.EveryoneRoleImmutable;

        // Check everything before changing anything, so an invalid value leaves the role untouched.
        if (name is not null && !Role.IsValidName(Role.NormalizeName(name)))
            throw new ArgumentException("Invalid role name.", nameof(name));
        if (!Role.IsValidColor(color))
            throw new ArgumentOutOfRangeException(nameof(color), color, "A colour must be an RGB value.");
        if (permissions is { } p && !Role.IsValidPermissions(p))
            throw new ArgumentOutOfRangeException(nameof(permissions), permissions, "Unknown permission flags.");

        if (name is not null)
            role.Rename(name);
        if (color is not null)
            role.ChangeColor(color);
        if (permissions is { } newPermissions)
            role.ChangePermissions(newPermissions);
        return RoleChangeOutcome.Done;
    }

    /// <summary>Whether <paramref name="position"/> is somewhere a role can move to: 1 up to the number of roles besides <c>@everyone</c>.</summary>
    public bool IsValidRolePosition(int position) => position >= 1 && position <= _roles.Count(r => !r.IsEveryone);

    /// <summary>
    /// Moves a role to <paramref name="position"/> (1..number of roles besides <c>@everyone</c>), renumbering the
    /// others so positions stay 0 (<c>@everyone</c>), 1, 2, … without gaps. <c>@everyone</c> doesn't move.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside 1..number of roles.</exception>
    public RoleChangeOutcome MoveRole(Guid roleId, int position)
    {
        var role = _roles.FirstOrDefault(r => r.Id == roleId);
        if (role is null)
            return RoleChangeOutcome.UnknownRole;
        if (role.IsEveryone)
            return RoleChangeOutcome.EveryoneRoleImmutable;
        if (!IsValidRolePosition(position))
            throw new ArgumentOutOfRangeException(nameof(position), position, "Not a position a role can move to.");

        var ordered = _roles.Where(r => !r.IsEveryone && r != role).OrderBy(r => r.Position).ToList();
        ordered.Insert(position - 1, role);
        Renumber(ordered);
        return RoleChangeOutcome.Done;
    }

    /// <summary>
    /// Deletes a role: its holders lose it, and the roles above it move down to close the gap. <c>@everyone</c> can't
    /// be deleted.
    /// </summary>
    public RoleChangeOutcome DeleteRole(Guid roleId)
    {
        var role = _roles.FirstOrDefault(r => r.Id == roleId);
        if (role is null)
            return RoleChangeOutcome.UnknownRole;
        if (role.IsEveryone)
            return RoleChangeOutcome.EveryoneRoleImmutable;

        foreach (var member in _members)
            member.Unassign(roleId);
        _roles.Remove(role);
        Renumber([.. _roles.Where(r => !r.IsEveryone).OrderBy(r => r.Position)]);
        return RoleChangeOutcome.Done;
    }

    private static void Renumber(IReadOnlyList<Role> rolesAboveEveryone)
    {
        for (var i = 0; i < rolesAboveEveryone.Count; i++)
            rolesAboveEveryone[i].MoveTo(i + 1);
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
