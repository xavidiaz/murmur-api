namespace Application.Auth;

/// <summary>Contract rules for credentials (API-AUTH-02).</summary>
public static class Credentials
{
    public const int PasswordMinLength = 8;
    public const int EmailMaxLength = 256;

    /// <summary>At least 8 characters with at least one digit; nothing else is required.</summary>
    public static bool IsValidPassword(string? password) =>
        password is { Length: >= PasswordMinLength } && password.Any(char.IsAsciiDigit);

    /// <summary>
    /// <c>local@domain</c>: exactly one <c>@</c>, both parts non-empty, no whitespace, at most 256 characters
    /// (the column size).
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (email is null || email.Length > EmailMaxLength || email.Any(char.IsWhiteSpace))
            return false;
        var at = email.IndexOf('@');
        return at > 0 && at < email.Length - 1 && email.IndexOf('@', at + 1) < 0;
    }
}
