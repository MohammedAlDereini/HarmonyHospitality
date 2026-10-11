using System.Security.Cryptography;
using System.Text;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;

namespace Harmony.Identity.Infrastructure.Passkeys;

/// <summary>
/// The pause between "create a passkey" and the browser's answer: the challenge Identity's handler issued (its attestation
/// state) lives 5 minutes in Redis under the account's id, so only that account's next answer can spend it. Spending is
/// ONE atomic step (add-if-absent on a "spent" mark named after the state), so two answers racing on two connections can
/// never both pass: the second finds the mark. Identity keeps this state in a cookie by default; the web app calls the
/// API with a Bearer token, so the server holds it instead.
/// </summary>
public sealed class PasskeyRegistrationHold
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string KeyPrefix = "identity:passkey-registration:";
    private const string SpentPrefix = "identity:passkey-registration-spent:";

    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;

    public PasskeyRegistrationHold(ICache<Harmony.Core.Cache.Providers.Redis> cache)
    {
        this.cache = cache;
    }

    /// <summary>Holds the challenge for this account; a new one replaces an older one still waiting.</summary>
    public async Task HoldAsync(Guid userId, string attestationState, CancellationToken cancellationToken)
    {
        var result = await this.cache.SaveAsync(KeyOf(userId), attestationState, Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException($"Storing the passkey registration failed ({result}).");
        }
    }

    /// <summary>
    /// The waiting challenge of this account, spent: null when none waits (expired, never asked) or when another call
    /// spent this very challenge a moment ago. A later HoldAsync makes a new challenge with its own mark.
    /// </summary>
    public async Task<string?> TakeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = KeyOf(userId);
        var response = await this.cache.GetAsync<string>(key, eCacheAccessLevel.Public, cancellationToken);
        var state = response?.Value;
        if (string.IsNullOrEmpty(state))
        {
            return null;
        }

        var claim = await this.cache.AddAsync(SpentPrefix + Fingerprint(state), "1", Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (claim != eCacheOperationResult.Success)
        {
            // Already spent by a parallel answer, or the store could not promise it was not: refused either way.
            return null;
        }

        await this.cache.DeleteAsync(key, eCacheAccessLevel.Public, cancellationToken);
        return state;
    }

    private static string KeyOf(Guid userId) => KeyPrefix + userId.ToString("D");

    // The state is JSON with the random challenge inside: its hash names it without repeating it in a key.
    internal static string Fingerprint(string state)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
}
