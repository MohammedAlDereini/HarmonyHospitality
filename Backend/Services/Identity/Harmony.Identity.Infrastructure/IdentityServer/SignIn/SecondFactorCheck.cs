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

    /// <summary>This challenge already completed its sign-in a moment ago: the same browser's doubled send walks through.</summary>
    AlreadyAccepted,

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
/// The second half of a two-factor sign-in on the sign-in pages (the only caller since BFF step 5 removed the mfa_otp
/// grant): the challenge the password step issued, then either the 6-digit code from the authenticator app or one recovery code.
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

        // A doubled send: the first copy finished this exact challenge a moment ago. The same browser (it holds the
        // challenge) repeating the same sign-in walks through, with no code spent, no wrong try and no false row.
        if (await this.challenges.DoneUserAsync(challenge, cancellationToken) is Guid doneUserId)
        {
            var doneUser = await this.userAccounts.FindByIdAcrossTenantsAsync(doneUserId, cancellationToken);
            if (doneUser is not null && doneUser.CanSignIn)
            {
                return new(SecondFactorStatus.AlreadyAccepted, doneUser);
            }
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

        var accepted = false;
        if (!string.IsNullOrEmpty(otp))
        {
            switch (await this.VerifyOtpAsync(user, otp, cancellationToken))
            {
                case OtpOutcome.Accepted:
                    accepted = true;
                    break;

                case OtpOutcome.Replayed:
                    // The other copy of a doubled send claimed this code a moment ago and is still finishing. Wait for
                    // its done-mark: when it lands, this is the same browser's same sign-in and it walks through. When
                    // nothing lands, the code really was replayed (another browser, an attacker): refused and counted.
                    for (var wait = 0; wait < 10; wait++)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                        if (await this.challenges.DoneUserAsync(challenge, cancellationToken) == user.Id)
                        {
                            return new(SecondFactorStatus.AlreadyAccepted, user);
                        }
                    }

                    break;
            }
        }
        else
        {
            accepted = (await this.userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode!.Trim())).Succeeded;
        }

        if (!accepted)
        {
            await this.userManager.AccessFailedAsync(user);
            var locked = await this.userManager.IsLockedOutAsync(user);
            this.logger.LogInformation("Second factor refused: wrong code for account {UserId}. LockedOut={LockedOut}", user.Id, locked);
            return new(locked ? SecondFactorStatus.LockedOut : SecondFactorStatus.CodeInvalid, user);
        }

        await this.userManager.ResetAccessFailedCountAsync(user);
        await this.challenges.ConsumeAsync(challenge!, cancellationToken);
        await this.challenges.MarkDoneAsync(challenge!, user.Id, cancellationToken);
        this.logger.LogInformation("Second factor accepted for account {UserId} in tenant {Tenant}.", user.Id, user.TenantId);

        // A backup code is an account recovery: the person is told, so a stolen code that gets used is noticed.
        if (string.IsNullOrEmpty(otp))
        {
            await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.BackupCodeUsed, cancellationToken);
        }

        return new(SecondFactorStatus.Accepted, user);
    }

    private async Task<OtpOutcome> VerifyOtpAsync(User user, string otp, CancellationToken cancellationToken)
    {
        otp = otp.Trim();
        if (otp.Length != 6 || !otp.All(char.IsAsciiDigit))
        {
            return OtpOutcome.Wrong;
        }

        // Claiming the code and asking "was it used?" are ONE atomic step (add-if-absent), so two sends can never both
        // spend one code. The claim comes first; a claim on a guess that then proves wrong only burns that wrong guess.
        var usedKey = UsedCodeKeyPrefix + user.Id.ToString("D") + ":" + otp;
        var claim = await this.cache.AddAsync(usedKey, "1", UsedCodeLifetime, eCacheAccessLevel.Public, cancellationToken);
        if (claim == eCacheOperationResult.KeyAlreadyExists)
        {
            this.logger.LogWarning("Second factor refused: replayed code for account {UserId}.", user.Id);
            return OtpOutcome.Replayed;
        }

        if (claim != eCacheOperationResult.Success)
        {
            // The store could not answer: the code is checked without replay protection rather than locking everyone out.
            this.logger.LogWarning("The used-code store did not answer ({Result}); replay protection is off for this one check.", claim);
        }

        return await this.userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, otp)
            ? OtpOutcome.Accepted
            : OtpOutcome.Wrong;
    }

    private enum OtpOutcome
    {
        Accepted,
        Wrong,
        /// <summary>This exact code was already claimed within its window.</summary>
        Replayed,
    }
}