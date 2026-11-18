using System.Security.Cryptography;

namespace Api.AcceptanceTests.Infrastructure;

/// <summary>Unique random values so tests never collide, in any order and in parallel.</summary>
public static class Unique
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>Random lowercase [a-z0-9] string.</summary>
    public static string Token(int length = 10) => RandomNumberGenerator.GetString(Alphabet, length);

    /// <summary>A valid, unused username (2–32 chars of [a-z0-9_.]), e.g. <c>u_k3j9x0a1b2c3</c>.</summary>
    public static string UserName(string prefix = "u") => $"{prefix}_{Token(12)}";

    /// <summary>A unique email address.</summary>
    public static string Email(string? local = null) => $"{local ?? Token(12)}@example.test";

    /// <summary>A unique, human-ish name for servers, channels, roles.</summary>
    public static string Name(string prefix = "test") => $"{prefix} {Token(8)}";
}
