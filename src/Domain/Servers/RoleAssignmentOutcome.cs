namespace Domain.Servers;

/// <summary>What assigning or unassigning a role did, or why it was refused.</summary>
public enum RoleAssignmentOutcome
{
    /// <summary>The role was assigned (or unassigned).</summary>
    Done,
    /// <summary>The member already had (or didn't have) the role; nothing changed.</summary>
    NoChange,
    /// <summary>The user isn't a member of this server.</summary>
    UnknownMember,
    /// <summary>No such role in this server (including roles of other servers).</summary>
    UnknownRole,
    /// <summary><c>@everyone</c> can't be assigned or unassigned: every member has it implicitly.</summary>
    EveryoneRole,
}
