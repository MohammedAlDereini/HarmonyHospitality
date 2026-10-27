using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.StepUpModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Infrastructure.Passkeys;

/// <summary>
/// "Confirm it is you" before something that lasts (a passkey added or removed, a request a second person approves). A
/// session can be hours old; the person proves themselves again, with the password, one of their passkeys, or their
/// company's sign-in (Microsoft's shape, NIST 800-63B-4 §4.1, OWASP: "do not rely only on possession of a long-lived
/// session cookie"). The proof is a StepUpAuthorizations row (design 3.5): minutes, set once, never extended. The
/// passkey proof needs its own challenge first, held in Redis like a registration's and spent atomically.
/// </summary>
public sealed class IdentityConfirmation
{
    public static readonly TimeSpan Lifetime = StepUpAuthorization.Lifetime;

    private const string ChallengePrefix = "identity:confirm-challenge:";
    private const string SpentPrefix = "identity:confirm-challenge-spent:";

    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;
    private readonly IStepUpAuthorizationRepository proofs;
    private readonly IFederatedAuthorityRepository authorities;
    private readonly TimeProvider clock;

    public IdentityConfirmation(ICache<Harmony.Core.Cache.Providers.Redis> cache, IStepUpAuthorizationRepository proofs, IFederatedAuthorityRepository authorities, TimeProvider clock)
    {
        this.cache = cache;
        this.proofs = proofs;
        this.authorities = authorities;
        this.clock = clock;
    }

    /// <summary>True while the account proved itself within the last few minutes.</summary>
    public async Task<bool> IsConfirmedAsync(Guid userId, CancellationToken cancellationToken)
        => await this.LiveAsync(userId, cancellationToken) is not null;

    /// <summary>The account's live proof, or null: what an approval request names as the maker's step-up.</summary>
    public Task<StepUpAuthorization?> LiveAsync(Guid userId, CancellationToken cancellationToken)
        => this.proofs.LiveOfAccountAsync(userId, this.clock.GetUtcNow(), cancellationToken);

    /// <summary>
    /// The account proved itself now (password, passkey or company). Staged on the caller's unit of work: the caller saves
    /// it with its trail row. The authority is the customer's local one unless the company's is named.
    /// </summary>
    public async Task MarkAsync(Guid tenantId, Guid userId, string how, CancellationToken cancellationToken, Guid? authorityId = null)
    {
        var authority = authorityId
            ?? (await this.authorities.OfTenantAsync(tenantId, cancellationToken)).First(a => a.Scheme == AuthorityScheme.Local).Id;
        await this.proofs.AddAsync(StepUpAuthorization.Prove(tenantId, userId, authority, how, this.clock.GetUtcNow()), cancellationToken);
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
