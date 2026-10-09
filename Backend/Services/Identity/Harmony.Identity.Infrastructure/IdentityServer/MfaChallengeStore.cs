using System.Buffers.Text;
using System.Security.Cryptography;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// The pause between the password and the second factor. A challenge is 32 random bytes that live in Redis for five
/// minutes and point at the account that passed the password. The sign-in that completes consumes it; a wrong code does
/// not burn it, the login lockout counts those. Redis is the shared place, so a second Identity instance sees it too.
/// </summary>
public sealed class MfaChallengeStore
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>How long a completed challenge still answers its own doubled send; longer than any straggler copy.</summary>
    public static readonly TimeSpan DoneLifetime = TimeSpan.FromSeconds(60);

    private const string KeyPrefix = "identity:mfa:";
    private const string DoneKeyPrefix = "identity:mfa-done:";
    private const int ChallengeLength = 43; // 32 bytes, base64url, no padding

    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;

    public MfaChallengeStore(ICache<Harmony.Core.Cache.Providers.Redis> cache)
    {
        this.cache = cache;
    }

    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var challenge = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var result = await this.cache.SaveAsync(KeyPrefix + challenge, userId.ToString("D"), Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException($"Storing the two-factor challenge failed ({result}).");
        }

        return challenge;
    }

    /// <summary>The account behind a live challenge, or null when it is unknown, expired or already used.</summary>
    public async Task<Guid?> PeekAsync(string? challenge, CancellationToken cancellationToken)
    {
        if (challenge is null || challenge.Length != ChallengeLength)
        {
            return null;
        }

        var response = await this.cache.GetAsync<string>(KeyPrefix + challenge, eCacheAccessLevel.Public, cancellationToken);
        return Guid.TryParse(response?.Value, out var userId) ? userId : null;
    }

    public async Task ConsumeAsync(string challenge, CancellationToken cancellationToken)
    {
        await this.cache.DeleteAsync(KeyPrefix + challenge, eCacheAccessLevel.Public, cancellationToken);
    }

    /// <summary>
    /// Remembers, briefly, that this challenge completed its sign-in and for whom. A failed write only costs smoothness:
    /// the doubled send would start the sign-in again instead of walking through.
    /// </summary>
    public async Task MarkDoneAsync(string challenge, Guid userId, CancellationToken cancellationToken)
    {
        _ = await this.cache.SaveAsync(DoneKeyPrefix + challenge, userId.ToString("D"), DoneLifetime, eCacheAccessLevel.Public, cancellationToken);
    }

    /// <summary>The account whose sign-in this challenge already completed a moment ago, or null.</summary>
    public async Task<Guid?> DoneUserAsync(string? challenge, CancellationToken cancellationToken)
    {
        if (challenge is null || challenge.Length != ChallengeLength)
        {
            return null;
        }

        var response = await this.cache.GetAsync<string>(DoneKeyPrefix + challenge, eCacheAccessLevel.Public, cancellationToken);
        return Guid.TryParse(response?.Value, out var userId) ? userId : null;
    }
}
