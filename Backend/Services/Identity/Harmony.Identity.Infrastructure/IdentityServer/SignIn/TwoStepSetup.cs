using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer.SignIn;

public enum TwoStepSetupStatus
{
    /// <summary>The app proved it holds the key: two-step is on and the backup codes were made.</summary>
    Enabled,

    /// <summary>No live setup for this browser: expired, finished, or the e-mail link was never opened here.</summary>
    NotOpened,

    CodeInvalid,
    LockedOut,
    AccountNotActive,
}

public sealed record TwoStepSetupResult(TwoStepSetupStatus Status, User? User, IReadOnlyList<string> RecoveryCodes);

/// <summary>
/// Two-step sign-in is required for everyone. A person who signs in without it set up gets a one-time link by e-mail; only a
/// browser that holds this setup's cookie AND opens that link sees the QR code. A thief who knows the password but not the
/// mailbox never sees it. A setup lives 15 minutes in Redis; the link is stored only as its SHA-256 hash.
/// Wrong codes count towards the account lockout, like at sign-in (5 → 5 minutes).
/// </summary>
public sealed class TwoStepSetup
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>The name the authenticator app shows for this account.</summary>
    public const string Issuer = "Harmony";

    public const int RecoveryCodeCount = 10;

    private const string KeyPrefix = "identity:2fa-setup:";
    private const int SecretLength = 43; // 32 bytes, base64url, no padding

    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;
    private readonly IUserAccountRepository userAccounts;
    private readonly UserManager<User> userManager;
    private readonly ILogger<TwoStepSetup> logger;

    public TwoStepSetup(ICache<Harmony.Core.Cache.Providers.Redis> cache, IUserAccountRepository userAccounts, UserManager<User> userManager, ILogger<TwoStepSetup> logger)
    {
        this.cache = cache;
        this.userAccounts = userAccounts;
        this.userManager = userManager;
        this.logger = logger;
    }

    /// <summary>Starts a setup: returns the browser's secret (for its cookie) and the link's secret (for the e-mail).</summary>
    public async Task<(string Browser, string Link)> StartAsync(User user, CancellationToken cancellationToken)
    {
        var browser = NewSecret();
        var link = NewSecret();
        await this.SaveAsync(browser, new Record(user.Id, Hash(link), Opened: false), cancellationToken);
        this.logger.LogInformation("Two-step setup started for account {UserId}: link sent by e-mail.", user.Id);
        return (browser, link);
    }

    /// <summary>The e-mail link was opened. True only when it belongs to this browser's setup.</summary>
    public async Task<bool> OpenAsync(string? browser, string? link, CancellationToken cancellationToken)
    {
        var record = await this.FindAsync(browser, cancellationToken);
        if (record is null || link is null || link.Length != SecretLength
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(record.LinkHash), Encoding.ASCII.GetBytes(Hash(link))))
        {
            this.logger.LogInformation("Two-step setup link refused: not this browser's, wrong, or expired.");
            return false;
        }

        await this.SaveAsync(browser!, record with { Opened = true }, cancellationToken);
        return true;
    }

    /// <summary>True while this browser's setup waits for its e-mail link.</summary>
    public async Task<bool> IsWaitingAsync(string? browser, CancellationToken cancellationToken)
        => await this.FindAsync(browser, cancellationToken) is { Opened: false };

    /// <summary>The account behind this browser's opened setup, or null.</summary>
    public async Task<User?> OpenedAccountAsync(string? browser, CancellationToken cancellationToken)
    {
        var record = await this.FindAsync(browser, cancellationToken);
        return record is { Opened: true } ? await this.userAccounts.FindByIdAcrossTenantsAsync(record.UserId, cancellationToken) : null;
    }

    /// <summary>The account's authenticator key (made once) and the otpauth address the QR code carries.</summary>
    public async Task<(string Key, string Uri)> KeyAsync(User user)
    {
        var key = await this.userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            if (!(await this.userManager.ResetAuthenticatorKeyAsync(user)).Succeeded)
            {
                throw new InvalidOperationException("The identity store refused to create the authenticator key.");
            }

            key = await this.userManager.GetAuthenticatorKeyAsync(user);
        }

        var account = user.Email ?? user.UserName!;
        return (key!, $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(account)}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6");
    }

    public async Task<TwoStepSetupResult> EnableAsync(string? browser, string? code, CancellationToken cancellationToken)
    {
        var user = await this.OpenedAccountAsync(browser, cancellationToken);
        if (user is null)
        {
            return new(TwoStepSetupStatus.NotOpened, null, []);
        }

        if (!user.CanSignIn)
        {
            return new(TwoStepSetupStatus.AccountNotActive, user, []);
        }

        if (await this.userManager.IsLockedOutAsync(user))
        {
            return new(TwoStepSetupStatus.LockedOut, user, []);
        }

        code = code?.Trim();
        var valid = code is { Length: 6 } && code.All(char.IsAsciiDigit)
            && await this.userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
        if (!valid)
        {
            await this.userManager.AccessFailedAsync(user);
            var locked = await this.userManager.IsLockedOutAsync(user);
            this.logger.LogInformation("Two-step setup: wrong code for account {UserId}. LockedOut={LockedOut}", user.Id, locked);
            return new(locked ? TwoStepSetupStatus.LockedOut : TwoStepSetupStatus.CodeInvalid, user, []);
        }

        await this.userManager.ResetAccessFailedCountAsync(user);
        var enabled = await this.userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enabled.Succeeded)
        {
            throw new InvalidOperationException("The identity store refused to turn two-step sign-in on.");
        }

        var codes = await this.userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await this.cache.DeleteAsync(KeyPrefix + browser, eCacheAccessLevel.Public, cancellationToken);
        this.logger.LogInformation("Two-step sign-in turned on for account {UserId} at sign-in.", user.Id);
        return new(TwoStepSetupStatus.Enabled, user, codes?.ToList() ?? []);
    }

    private async Task<Record?> FindAsync(string? browser, CancellationToken cancellationToken)
    {
        if (browser is null || browser.Length != SecretLength)
        {
            return null;
        }

        var response = await this.cache.GetAsync<string>(KeyPrefix + browser, eCacheAccessLevel.Public, cancellationToken);
        var parts = response?.Value?.Split('|');
        return parts is { Length: 3 } && Guid.TryParse(parts[0], out var userId)
            ? new Record(userId, parts[1], parts[2] == "1")
            : null;
    }

    private async Task SaveAsync(string browser, Record record, CancellationToken cancellationToken)
    {
        var value = $"{record.UserId:D}|{record.LinkHash}|{(record.Opened ? "1" : "0")}";
        var result = await this.cache.SaveAsync(KeyPrefix + browser, value, Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException($"Storing the two-step setup failed ({result}).");
        }
    }

    private static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(secret)));

    private sealed record Record(Guid UserId, string LinkHash, bool Opened);
}
