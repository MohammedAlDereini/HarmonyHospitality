using System.Security.Cryptography;
using System.Text;

namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// The secret a service principal signs in with. 256 random bits, shown once; only its SHA-256
/// digest is stored, so a database leak yields nothing usable. Compared in constant time.
/// </summary>
public static class ServiceSecret
{
    private const int SecretBytes = 32;

    public static string Generate()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Digest(string secret)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public static bool Matches(string? secret, string? digest)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(digest))
        {
            return false;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(digest);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}