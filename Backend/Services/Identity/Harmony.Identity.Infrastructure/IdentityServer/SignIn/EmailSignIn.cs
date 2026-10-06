using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer.SignIn;

/// <summary>
/// The second step for people who chose the e-mail link (like Mews): after the right password, a one-time link goes to the
/// account's e-mail and signs in only the browser that typed the password. It lives 10 minutes in Redis, works once, and is
/// stored only as its SHA-256 hash.
/// </summary>
public sealed class EmailSignIn
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private const string KeyPrefix = "identity:2fa-email:";
    private const int SecretLength = 43; // 32 bytes, base64url, no padding

    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;
    private readonly IUserAccountRepository userAccounts;
    private readonly ILogger<EmailSignIn> logger;

    public EmailSignIn(ICache<Harmony.Core.Cache.Providers.Redis> cache, IUserAccountRepository userAccounts, ILogger<EmailSignIn> logger)
    {
        this.cache = cache;
        this.userAccounts = userAccounts;
        this.logger = logger;
    }

    /// <summary>Returns the browser's secret (for its cookie) and the link's secret (for the e-mail).</summary>
    public async Task<(string Browser, string Link)> StartAsync(User user, CancellationToken cancellationToken)
    {
        var browser = NewSecret();
        var link = NewSecret();
        var result = await this.cache.SaveAsync(KeyPrefix + browser, $"{user.Id:D}|{Hash(link)}", Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException($"Storing the sign-in link failed ({result}).");
        }

        this.logger.LogInformation("Sign-in link sent by e-mail for account {UserId}.", user.Id);
        return (browser, link);
    }

    /// <summary>
    /// The person behind this browser's link, or null: wrong browser, wrong or used link, expired, or the account can no
    /// longer sign in. The link works once.
    /// </summary>
    public async Task<User?> CompleteAsync(string? browser, string? link, CancellationToken cancellationToken)
    {
        if (browser is not { Length: SecretLength } || link is not { Length: SecretLength })
        {
            return null;
        }

        var response = await this.cache.GetAsync<string>(KeyPrefix + browser, eCacheAccessLevel.Public, cancellationToken);
        var parts = response?.Value?.Split('|');
        if (parts is not { Length: 2 } || !Guid.TryParse(parts[0], out var userId)
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(parts[1]), Encoding.ASCII.GetBytes(Hash(link))))
        {
            this.logger.LogInformation("Sign-in link refused: not this browser's, wrong, used, or expired.");
            return null;
        }

        await this.cache.DeleteAsync(KeyPrefix + browser, eCacheAccessLevel.Public, cancellationToken);
        var user = await this.userAccounts.FindByIdAcrossTenantsAsync(userId, cancellationToken);
        return user is { CanSignIn: true, TwoFactorEnabled: true } ? user : null;
    }

    private static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(secret)));
}
