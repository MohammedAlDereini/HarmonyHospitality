using System.Security.Cryptography;
using System.Text;
using Harmony.Core.Exceptions;
using Harmony.Core.Models;
using Harmony.Identity.Domain.Common;

namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// The secret a service principal signs in with. 256 random bits, shown once; only its SHA-256
/// digest is stored, so a database leak yields nothing usable. Compared in constant time.
/// </summary>
public static class ServiceSecret
{
    private const int SecretBytes = 32;

    /// <summary>A supplied secret must be at least this long: 32 base64url characters carry 192 bits.</summary>
    public const int MinimumLength = 32;

    public static string Generate()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Digest(string secret)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>Refuses a supplied secret that is missing, short, or not a single line.</summary>
    public static void EnsureUsable(string? secret)
    {
        if (secret is null || secret.Length < MinimumLength || secret.Any(char.IsWhiteSpace))
        {
            throw new BusinessException(
                $"A service secret must be one line of at least {MinimumLength} characters.",
                Error.New(IdentityErrorCodes.InvalidServiceSecret));
        }
    }

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
