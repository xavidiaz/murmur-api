namespace Domain.Servers;

/// <summary>What a role change did, or why it was refused (nothing changes when refused).</summary>
public enum RoleChangeOutcome
{
    Done,
    /// <summary>No such role in this server.</summary>
    UnknownRole,
    /// <summary><c>@everyone</c> can't be renamed, moved or deleted (its permissions and colour can change).</summary>
    EveryoneRoleImmutable,
}
