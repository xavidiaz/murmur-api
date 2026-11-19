using System.Security.Cryptography;

namespace Domain.Invites;

/// <summary>Generates invite codes: 8 characters of <c>[0-9A-Za-z]</c> (62^8 ≈ 2.2 × 10^14 codes).</summary>
public static class InviteCode
{
    public const int Length = 8;
    public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// A fresh code from the cryptographic RNG. <see cref="RandomNumberGenerator.GetString"/> picks each character
    /// uniformly (no modulo bias). Uniqueness against stored codes is the caller's job.
    /// </summary>
    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);
}
