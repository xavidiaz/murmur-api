namespace Domain.Users;

/// <summary>Rules for a user's public identity (username and display name).</summary>
public static class UserProfile
{
    /// <summary>Usernames are stored in lowercase, so they are unique regardless of capitals.</summary>
    public static string NormalizeUserName(string userName) => userName.ToLowerInvariant();

    /// <summary>The display name trimmed, or the username when none was chosen.</summary>
    public static string ResolveDisplayName(string? displayName, string userName) =>
        string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim();
}
