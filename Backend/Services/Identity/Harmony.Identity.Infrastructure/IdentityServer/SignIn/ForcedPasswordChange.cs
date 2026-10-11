using System.Buffers.Text;
using System.Security.Cryptography;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer.SignIn;

/// <summary>
/// A sign-in on hold: who proved themselves, how ("pwd", "mfa", or "swk,mfa" for a passkey: the amr values, comma-joined),
/// the trail line the finished sign-in writes, and its detail (the passkey's name; null for the other methods).
/// </summary>
public sealed record HeldPasswordChange(User User, string Method, string TrailAction, string? Detail = null);

/// <summary>
/// The pause in the sign-in walk for a password somebody else chose (a new account's initial password): the person has
/// proved who they are but is NOT signed in yet — the change page finishes the sign-in. The pause lives 10 minutes in
/// Redis under a random secret the browser keeps in a cookie, like the two-step challenge, and it is only ever found
/// while the account still must change its password.
/// </summary>
public sealed class ForcedPasswordChange
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private const string KeyPrefix = "identity:password-change:";
    private const int SecretLength = 43; // 32 bytes, base64url, no padding

    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;
    private readonly IUserAccountRepository userAccounts;
    private readonly ILogger<ForcedPasswordChange> logger;

    public ForcedPasswordChange(ICache<Harmony.Core.Cache.Providers.Redis> cache, IUserAccountRepository userAccounts, ILogger<ForcedPasswordChange> logger)
    {
        this.cache = cache;
        this.userAccounts = userAccounts;
        this.logger = logger;
    }

    /// <summary>Puts a proved sign-in on hold; returns the secret for the browser's cookie.</summary>
    public async Task<string> HoldAsync(User user, string method, string trailAction, CancellationToken cancellationToken, string? detail = null)
    {
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var result = await this.cache.SaveAsync(KeyPrefix + secret, $"{user.Id:D}|{method}|{trailAction}|{detail}", Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException($"Storing the password-change hold failed ({result}).");
        }

        this.logger.LogInformation("Sign-in on hold for account {UserId}: the initial password must be replaced first.", user.Id);
        return secret;
    }

    /// <summary>The held sign-in behind this browser's secret, or null: unknown, expired, released, or the account no longer must change.</summary>
    public async Task<HeldPasswordChange?> FindAsync(string? secret, CancellationToken cancellationToken)
    {
        if (secret is null || secret.Length != SecretLength)
        {
            return null;
        }

        var response = await this.cache.GetAsync<string>(KeyPrefix + secret, eCacheAccessLevel.Public, cancellationToken);
        var parts = response?.Value?.Split('|', 4);
        if (parts is not { Length: 4 } || !Guid.TryParse(parts[0], out var userId))
        {
            return null;
        }

        var user = await this.userAccounts.FindByIdAcrossTenantsAsync(userId, cancellationToken);
        return user is { MustChangePassword: true } ? new HeldPasswordChange(user, parts[1], parts[2], parts[3].Length > 0 ? parts[3] : null) : null;
    }

    public async Task ReleaseAsync(string secret, CancellationToken cancellationToken)
    {
        await this.cache.DeleteAsync(KeyPrefix + secret, eCacheAccessLevel.Public, cancellationToken);
    }

    /// <summary>The account as it is NOW (after the change its SecurityVersion moved): the sign-in cookie must carry that one.</summary>
    public Task<User?> ReloadAsync(Guid userId, CancellationToken cancellationToken)
        => this.userAccounts.FindByIdAcrossTenantsAsync(userId, cancellationToken);
}
