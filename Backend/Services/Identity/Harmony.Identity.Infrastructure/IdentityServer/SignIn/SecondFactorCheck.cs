using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer.SignIn;

public enum SecondFactorStatus
{
    Accepted,

    /// <summary>Neither or both of a code and a recovery code were given: exactly one proof is expected.</summary>
    OneProofRequired,

    /// <summary>The challenge is unknown, expired or already used: the sign-in starts again.</summary>
    ChallengeInvalid,

    AccountNotActive,
    LockedOut,
    CodeInvalid,
}

public sealed record SecondFactorResult(SecondFactorStatus Status, User? User);

/// <summary>
/// The second half of a two-factor sign-in, in one place for the sign-in page and (until it is removed) the mfa_otp grant:
/// the challenge the password step issued, then either the 6-digit code from the authenticator app or one recovery code.
/// Wrong codes count towards the same lockout as wrong passwords (5 → 5 minutes). A code accepted once is refused for
/// the rest of its window (RFC 6238 §5.2: a verifier must not accept a replay). On success the challenge is consumed.
/// </summary>
public sealed class SecondFactorCheck
{
    // Identity accepts a code for ±2 steps of 30 s; a used code is remembered a little longer than that.
    private const string UsedCodeKeyPrefix = "identity:mfa-used:";
    private static readonly TimeSpan UsedCodeLifetime = TimeSpan.FromMinutes(3);

    private readonly IUserAccountRepository userAccounts;
    private readonly UserManager<User> userManager;
    private readonly MfaChallengeStore challenges;
    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;
    private readonly IAccountMailer mailer;
    private readonly ILogger<SecondFactorCheck> logger;

    public SecondFactorCheck(
        IUserAccountRepository userAccounts,
        UserManager<User> userManager,
        MfaChallengeStore challenges,
        ICache<Harmony.Core.Cache.Providers.Redis> cache,
        IAccountMailer mailer,
        ILogger<SecondFactorCheck> logger)
    {
        this.userAccounts = userAccounts;
        this.userManager = userManager;
        this.challenges = challenges;
        this.cache = cache;
        this.mailer = mailer;
        this.logger = logger;
    }

    /// <summary>True while the challenge is live: the two-step page shows its form only then.</summary>
    public async Task<bool> IsLiveAsync(string? challenge, CancellationToken cancellationToken)
        => await this.challenges.PeekAsync(challenge, cancellationToken) is not null;

    public async Task<SecondFactorResult> VerifyAsync(string? challenge, string? otp, string? recoveryCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(otp) == string.IsNullOrEmpty(recoveryCode))
        {
            return new(SecondFactorStatus.OneProofRequired, null);
        }

        var userId = await this.challenges.PeekAsync(challenge, cancellationToken);
        if (userId is null)
        {
            this.logger.LogInformation("Second factor refused: unknown or expired challenge.");
            return new(SecondFactorStatus.ChallengeInvalid, null);
        }

        var user = await this.userAccounts.FindByIdAcrossTenantsAsync(userId.Value, cancellationToken);

        if (user is null || !user.CanSignIn || !user.TwoFactorEnabled)
        {
            this.logger.LogInformation("Second factor refused: account {UserId} cannot sign in.", userId);
            return new(SecondFactorStatus.AccountNotActive, user);
        }

        if (await this.userManager.IsLockedOutAsync(user))
        {
            this.logger.LogWarning("Second factor refused: account {UserId} is locked out.", user.Id);
            return new(SecondFactorStatus.LockedOut, user);
        }

        var accepted = !string.IsNullOrEmpty(otp)
            ? await this.VerifyOtpAsync(user, otp, cancellationToken)
            : (await this.userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode!.Trim())).Succeeded;

        if (!accepted)
        {
            await this.userManager.AccessFailedAsync(user);
            var locked = await this.userManager.IsLockedOutAsync(user);
            this.logger.LogInformation("Second factor refused: wrong code for account {UserId}. LockedOut={LockedOut}", user.Id, locked);
            return new(locked ? SecondFactorStatus.LockedOut : SecondFactorStatus.CodeInvalid, user);
        }

        await this.userManager.ResetAccessFailedCountAsync(user);
        await this.challenges.ConsumeAsync(challenge!, cancellationToken);
        this.logger.LogInformation("Second factor accepted for account {UserId} in tenant {Tenant}.", user.Id, user.TenantId);

        // A backup code is an account recovery: the person is told, so a stolen code that gets used is noticed.
        if (string.IsNullOrEmpty(otp))
        {
            await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.BackupCodeUsed, cancellationToken);
        }

        return new(SecondFactorStatus.Accepted, user);
    }

    private async Task<bool> VerifyOtpAsync(User user, string otp, CancellationToken cancellationToken)
    {
        otp = otp.Trim();
        if (otp.Length != 6 || !otp.All(char.IsAsciiDigit))
        {
            return false;
        }

        var usedKey = UsedCodeKeyPrefix + user.Id.ToString("D") + ":" + otp;
        if (await this.cache.ExistsAsync(usedKey, eCacheAccessLevel.Public, cancellationToken) == eCacheOperationResult.Success)
        {
            this.logger.LogWarning("Second factor refused: replayed code for account {UserId}.", user.Id);
            return false;
        }

        if (!await this.userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, otp))
        {
            return false;
        }

        await this.cache.SaveAsync(usedKey, "1", UsedCodeLifetime, eCacheAccessLevel.Public, cancellationToken);
        return true;
    }
}