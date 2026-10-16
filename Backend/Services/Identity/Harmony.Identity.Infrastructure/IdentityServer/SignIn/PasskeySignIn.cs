using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer.SignIn;

public enum PasskeySignInStatus
{
    /// <summary>The passkey verified, the account is active and not locked out: the person is signed in.</summary>
    Accepted,

    /// <summary>The challenge is unknown, expired or already answered: the sign-in starts again.</summary>
    ChallengeInvalid,

    /// <summary>The answer did not verify (origin, signature, flags, counter), or the passkey is gone.</summary>
    Refused,

    AccountNotActive,
    /// <summary>The account is fine, its company is not: the tenant is suspended or terminated.</summary>
    TenantNotActive,
    LockedOut,
}

/// <summary>
/// The outcome, and the account when it is known: the one the answer verified for, or, for a refused answer, the one
/// that holds the passkey the answer named (so its trail says somebody tried with its key). PasskeyName is the key that
/// signed in, for the trail line. UnknownCredentialId is set when the answer named a key nobody holds any more: the page
/// hands it to the browser's signal method, so the device stops offering a dead key.
/// </summary>
public sealed record PasskeySignInResult(PasskeySignInStatus Status, User? User, string? PasskeyName = null, string? UnknownCredentialId = null);

/// <summary>
/// Sign-in with a passkey on the sign-in page, in two halves. First the challenge: Identity's handler builds the
/// WebAuthn request (no account named, so the browser offers the person's discoverable passkeys) and its state is held
/// 5 minutes in Redis under a random secret the browser keeps in a cookie, like the two-step challenge. Then the answer:
/// the challenge is spent in one atomic step (add-if-absent), so a doubled or replayed answer meets no challenge, then
/// Identity's handler checks the challenge, the origin, the user verification, the signature and the counter, and only
/// an active, unlocked account signs in. Nothing here counts towards the lockout: a passkey answer cannot be guessed, it
/// is signed over 32 random bytes.
/// </summary>
public sealed class PasskeySignIn
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>A real answer is a few hundred bytes; 64 KB bounds what the page accepts.</summary>
    public const int MaxCredentialJsonLength = 65536;

    private const string KeyPrefix = "identity:passkey-sign-in:";
    private const string SpentPrefix = "identity:passkey-sign-in-spent:";
    private const int SecretLength = 43; // 32 bytes, base64url, no padding

    private readonly IPasskeyHandler<User> handler;
    private readonly UserManager<User> userManager;
    private readonly IUserPasskeyRepository passkeys;
    private readonly IUserAccountRepository userAccounts;
    private readonly ITenantRepository tenants;
    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;
    private readonly ILogger<PasskeySignIn> logger;

    public PasskeySignIn(
        IPasskeyHandler<User> handler,
        UserManager<User> userManager,
        IUserPasskeyRepository passkeys,
        IUserAccountRepository userAccounts,
        ITenantRepository tenants,
        ICache<Harmony.Core.Cache.Providers.Redis> cache,
        ILogger<PasskeySignIn> logger)
    {
        this.handler = handler;
        this.userManager = userManager;
        this.passkeys = passkeys;
        this.userAccounts = userAccounts;
        this.tenants = tenants;
        this.cache = cache;
        this.logger = logger;
    }

    /// <summary>The challenge for the browser (WebAuthn request options as JSON) and the secret for the browser's cookie.</summary>
    public async Task<(string OptionsJson, string Secret)> StartAsync(HttpContext http, CancellationToken cancellationToken)
    {
        var options = await this.handler.MakeRequestOptionsAsync(user: null!, http);

        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var result = await this.cache.SaveAsync(KeyPrefix + secret, options.AssertionState, Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException($"Storing the passkey challenge failed ({result}).");
        }

        return (options.RequestOptionsJson, secret);
    }

    public async Task<PasskeySignInResult> FinishAsync(string? secret, string? credentialJson, HttpContext http, CancellationToken cancellationToken)
    {
        if (secret is not { Length: SecretLength })
        {
            return new(PasskeySignInStatus.ChallengeInvalid, null);
        }

        // Spending comes first and is ONE step: two answers to one challenge, however close, cannot both pass.
        var claim = await this.cache.AddAsync(SpentPrefix + secret, "1", Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (claim != eCacheOperationResult.Success)
        {
            this.logger.LogInformation("Passkey sign-in refused: the challenge was already answered.");
            return new(PasskeySignInStatus.ChallengeInvalid, null);
        }

        var key = KeyPrefix + secret;
        var state = (await this.cache.GetAsync<string>(key, eCacheAccessLevel.Public, cancellationToken))?.Value;
        await this.cache.DeleteAsync(key, eCacheAccessLevel.Public, cancellationToken);
        if (string.IsNullOrEmpty(state))
        {
            this.logger.LogInformation("Passkey sign-in refused: unknown or expired challenge.");
            return new(PasskeySignInStatus.ChallengeInvalid, null);
        }

        if (string.IsNullOrEmpty(credentialJson) || credentialJson.Length > MaxCredentialJsonLength)
        {
            return new(PasskeySignInStatus.Refused, null);
        }

        // The hold is spent: whatever happens next, this challenge answers nothing again.

        PasskeyAssertionResult<User> assertion;
        try
        {
            assertion = await this.handler.PerformAssertionAsync(new PasskeyAssertionContext
            {
                HttpContext = http,
                CredentialJson = credentialJson,
                AssertionState = state,
            });
        }
        catch (Exception exception) when (exception is PasskeyException or JsonException or ArgumentException or FormatException)
        {
            this.logger.LogInformation("Passkey sign-in refused: the answer could not be read ({Reason}).", exception.Message);
            return new(PasskeySignInStatus.Refused, await this.HolderOfAsync(credentialJson, cancellationToken));
        }

        if (!assertion.Succeeded)
        {
            this.logger.LogInformation("Passkey sign-in refused: {Reason}", assertion.Failure.Message);
            var holder = await this.HolderOfAsync(credentialJson, cancellationToken);
            return new(PasskeySignInStatus.Refused, holder, UnknownCredentialId: holder is null ? CredentialIdOf(credentialJson) : null);
        }

        var user = assertion.User;
        if (!user.CanSignIn)
        {
            this.logger.LogInformation("Passkey sign-in refused: account {UserId} is {State}.", user.Id, user.State);
            return new(PasskeySignInStatus.AccountNotActive, user);
        }

        if (await this.tenants.StatusOfAsync(user.TenantId, cancellationToken) != TenantStatus.Active)
        {
            this.logger.LogInformation("Passkey sign-in refused: the tenant of account {UserId} is not active.", user.Id);
            return new(PasskeySignInStatus.TenantNotActive, user);
        }

        if (await this.userManager.IsLockedOutAsync(user))
        {
            this.logger.LogWarning("Passkey sign-in refused: account {UserId} is locked out.", user.Id);
            return new(PasskeySignInStatus.LockedOut, user);
        }

        // The counter and the backup state the authenticator reported now, on the row Identity verified against.
        var row = await this.passkeys.FindByCredentialAsync(user.Id, assertion.Passkey.CredentialId, cancellationToken);
        if (row is null)
        {
            this.logger.LogWarning("Passkey sign-in refused: the passkey of account {UserId} was removed meanwhile.", user.Id);
            return new(PasskeySignInStatus.Refused, user);
        }

        row.Used(assertion.Passkey.SignCount, assertion.Passkey.IsBackedUp, DateTimeOffset.UtcNow);
        await this.passkeys.SaveChangesAsync(cancellationToken);
        await this.userManager.ResetAccessFailedCountAsync(user);

        this.logger.LogInformation("Passkey sign-in accepted for account {UserId} in tenant {Tenant}.", user.Id, user.TenantId);
        return new(PasskeySignInStatus.Accepted, user, row.Name);
    }

    /// <summary>The credential id the answer names, base64url as the browser wrote it, or null when the answer has none readable.</summary>
    private static string? CredentialIdOf(string credentialJson)
    {
        try
        {
            using var document = JsonDocument.Parse(credentialJson);
            if (!document.RootElement.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = id.GetString()!;
            return value.Length is > 0 and <= 1400 && Base64Url.IsValid(value) ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The account holding the passkey a refused answer named, or null when the answer named no known passkey. The
    /// answer proved nothing, so this is only for the trail: that account sees "somebody tried with your key" (a cloned
    /// key, a stale device), never a sign-in.
    /// </summary>
    private async Task<User?> HolderOfAsync(string credentialJson, CancellationToken cancellationToken)
    {
        byte[] credentialId;
        try
        {
            using var document = JsonDocument.Parse(credentialJson);
            if (!document.RootElement.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            credentialId = Base64Url.DecodeFromChars(id.GetString()!);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return null;
        }

        if (credentialId.Length is 0 or > UserPasskey.CredentialIdMaxLength)
        {
            return null;
        }

        var passkey = await this.passkeys.FindByCredentialIdAsync(credentialId, cancellationToken);
        return passkey is null ? null : await this.userAccounts.FindByIdAcrossTenantsAsync(passkey.UserAccountId, cancellationToken);
    }
}
