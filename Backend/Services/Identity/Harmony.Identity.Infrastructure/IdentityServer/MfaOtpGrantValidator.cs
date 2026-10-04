using System.Globalization;
using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// The second half of a two-factor sign-in, as a Duende extension grant (the shape Auth0 calls mfa-otp): grant_type=mfa_otp,
/// mfa_token = the challenge the password grant returned, and either otp = the 6-digit code from the authenticator app or
/// recovery_code = one of the codes shown when two-factor was enabled. Wrong codes count towards the same lockout as wrong
/// passwords (5 → 5 minutes). A code accepted once is refused for the rest of its window (RFC 6238: a verifier must not
/// accept a replay). On success the challenge is consumed and the tokens come out with amr=mfa.
/// </summary>
public sealed class MfaOtpGrantValidator : IExtensionGrantValidator
{
    public const string GrantTypeName = "mfa_otp";
    public const string MfaTokenParameter = "mfa_token";
    public const string OtpParameter = "otp";
    public const string RecoveryCodeParameter = "recovery_code";
    public const string MfaTokenInvalid = "mfa_token_invalid";
    public const string OtpInvalid = "otp_invalid";
    public const string OneCodeRequired = "otp_or_recovery_code_required";

    // Identity accepts a code for ±2 steps of 30 s; a used code is remembered a little longer than that.
    private const string UsedCodeKeyPrefix = "identity:mfa-used:";
    private static readonly TimeSpan UsedCodeLifetime = TimeSpan.FromMinutes(3);

    private readonly IdentityDbContext dbContext;
    private readonly UserManager<User> userManager;
    private readonly MfaChallengeStore challenges;
    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;
    private readonly ILogger<MfaOtpGrantValidator> logger;

    public MfaOtpGrantValidator(
        IdentityDbContext dbContext,
        UserManager<User> userManager,
        MfaChallengeStore challenges,
        ICache<Harmony.Core.Cache.Providers.Redis> cache,
        ILogger<MfaOtpGrantValidator> logger)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.challenges = challenges;
        this.cache = cache;
        this.logger = logger;
    }

    public string GrantType => GrantTypeName;

    public async Task ValidateAsync(ExtensionGrantValidationContext context, CancellationToken cancellationToken)
    {
        var raw = context.Request.Raw;
        var challenge = raw[MfaTokenParameter];
        var otp = raw[OtpParameter];
        var recoveryCode = raw[RecoveryCodeParameter];

        // Exactly one proof: a code from the app, or a recovery code.
        if (string.IsNullOrEmpty(otp) == string.IsNullOrEmpty(recoveryCode))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, OneCodeRequired);
            return;
        }

        var userId = await this.challenges.PeekAsync(challenge, cancellationToken);
        if (userId is null)
        {
            this.logger.LogInformation("Second factor refused: unknown or expired challenge.");
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, MfaTokenInvalid);
            return;
        }

        // Same pipeline position as the password grant: no tenant is known yet, so the lookup drops the tenant filter.
        var user = await this.dbContext.UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.CanSignIn || !user.TwoFactorEnabled)
        {
            this.logger.LogInformation("Second factor refused: account {UserId} cannot sign in.", userId);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, HarmonyPasswordValidator.AccountNotActive);
            return;
        }

        if (await this.userManager.IsLockedOutAsync(user))
        {
            this.logger.LogWarning("Second factor refused: account {UserId} is locked out.", user.Id);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, HarmonyPasswordValidator.LockedOut);
            return;
        }

        var accepted = otp is not null
            ? await this.VerifyOtpAsync(user, otp, cancellationToken)
            : (await this.userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode!)).Succeeded;

        if (!accepted)
        {
            await this.userManager.AccessFailedAsync(user);
            var locked = await this.userManager.IsLockedOutAsync(user);
            this.logger.LogInformation("Second factor refused: wrong code for account {UserId}. LockedOut={LockedOut}", user.Id, locked);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, locked ? HarmonyPasswordValidator.LockedOut : OtpInvalid);
            return;
        }

        await this.userManager.ResetAccessFailedCountAsync(user);
        await this.challenges.ConsumeAsync(challenge!, cancellationToken);
        this.logger.LogInformation("Second factor accepted for account {UserId} in tenant {Tenant}.", user.Id, user.TenantId);

        // Same subject shape as the password grant (see HarmonyPasswordValidator); amr says how strongly the person signed in.
        context.Result = new GrantValidationResult(
            user.Id.ToString("D"),
            "mfa",
            [new Claim(PlatformClaimTypes.SecurityVersion, user.SecurityVersion.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<bool> VerifyOtpAsync(User user, string otp, CancellationToken cancellationToken)
    {
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
