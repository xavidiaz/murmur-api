namespace Domain.Users;

/// <summary>Rules for a user's public identity (username and display name).</summary>
public static class UserProfile
{
    public const int UserNameMinLength = 2;
    public const int UserNameMaxLength = 32;
    public const int DisplayNameMinLength = 1;
    public const int DisplayNameMaxLength = 32;

    /// <summary>Usernames are stored in lowercase, so they are unique regardless of capitals.</summary>
    public static string NormalizeUserName(string userName) => userName.ToLowerInvariant();

    /// <summary>A normalized username: 2–32 characters of <c>[a-z0-9_.]</c>.</summary>
    public static bool IsValidUserName(string userName) =>
        userName.Length is >= UserNameMinLength and <= UserNameMaxLength
        && userName.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '.');

    /// <summary>The display name trimmed, or the username when none was chosen.</summary>
    public static string ResolveDisplayName(string? displayName, string userName) =>
        string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim();

    /// <summary>A resolved (trimmed) display name: 1–32 characters.</summary>
    public static bool IsValidDisplayName(string displayName) =>
        displayName.Length is >= DisplayNameMinLength and <= DisplayNameMaxLength;
}
