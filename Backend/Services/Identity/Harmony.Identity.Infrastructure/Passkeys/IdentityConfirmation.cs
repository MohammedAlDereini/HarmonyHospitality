using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;

namespace Harmony.Identity.Infrastructure.Passkeys;

/// <summary>
/// "Prove it is you" before a passkey is added or removed. A session can be hours old; a passkey is a permanent key, so
/// the person proves themselves again, with the password or with one of their passkeys (Microsoft's shape, NIST
/// 800-63B-4 §4.1 on binding an authenticator, OWASP: "do not rely only on possession of a long-lived session cookie").
/// The proof is a mark in Redis for the account, good for 5 minutes, enough to add a key and name it. The passkey proof
/// needs its own challenge first, held like a registration's and spent atomically.
/// </summary>
public sealed class IdentityConfirmation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string ConfirmedPrefix = "identity:confirmed:";
    private const string ChallengePrefix = "identity:confirm-challenge:";
    private const string SpentPrefix = "identity:confirm-challenge-spent:";

    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;

    public IdentityConfirmation(ICache<Harmony.Core.Cache.Providers.Redis> cache)
    {
        this.cache = cache;
    }

    /// <summary>True while the account proved itself within the last 5 minutes.</summary>
    public async Task<bool> IsConfirmedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var response = await this.cache.GetAsync<string>(ConfirmedPrefix + userId.ToString("D"), eCacheAccessLevel.Public, cancellationToken);
        return !string.IsNullOrEmpty(response?.Value);
    }

    /// <summary>The account proved itself now, by the password or by a passkey.</summary>
    public async Task MarkAsync(Guid userId, string how, CancellationToken cancellationToken)
    {
        var result = await this.cache.SaveAsync(ConfirmedPrefix + userId.ToString("D"), how, Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException($"Storing the identity confirmation failed ({result}).");
        }
    }

    /// <summary>Holds the passkey challenge issued for this account's proof; a new one replaces an older one still waiting.</summary>
    public async Task HoldChallengeAsync(Guid userId, string assertionState, CancellationToken cancellationToken)
    {
        var result = await this.cache.SaveAsync(ChallengePrefix + userId.ToString("D"), assertionState, Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException($"Storing the confirmation challenge failed ({result}).");
        }
    }

    /// <summary>The waiting challenge of this account, spent in one atomic step; null when none waits or it was spent a moment ago.</summary>
    public async Task<string?> TakeChallengeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = ChallengePrefix + userId.ToString("D");
        var state = (await this.cache.GetAsync<string>(key, eCacheAccessLevel.Public, cancellationToken))?.Value;
        if (string.IsNullOrEmpty(state))
        {
            return null;
        }

        var claim = await this.cache.AddAsync(SpentPrefix + PasskeyRegistrationHold.Fingerprint(state), "1", Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (claim != eCacheOperationResult.Success)
        {
            return null;
        }

        await this.cache.DeleteAsync(key, eCacheAccessLevel.Public, cancellationToken);
        return state;
    }
}
