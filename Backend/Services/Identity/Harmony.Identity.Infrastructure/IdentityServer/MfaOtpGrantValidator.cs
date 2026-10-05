using System.Globalization;
using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// The second half of a two-factor sign-in on the password grant, as a Duende extension grant (the shape Auth0 calls
/// mfa-otp): grant_type=mfa_otp, mfa_token = the challenge the password grant returned, and either otp or recovery_code.
/// The rules live in <see cref="SecondFactorCheck"/>, shared with the sign-in page. To be removed with the password grant.
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

    private readonly SecondFactorCheck secondFactorCheck;

    public MfaOtpGrantValidator(SecondFactorCheck secondFactorCheck)
    {
        this.secondFactorCheck = secondFactorCheck;
    }

    public string GrantType => GrantTypeName;

    public async Task ValidateAsync(ExtensionGrantValidationContext context, CancellationToken cancellationToken)
    {
        var raw = context.Request.Raw;
        var result = await this.secondFactorCheck.VerifyAsync(raw[MfaTokenParameter], raw[OtpParameter], raw[RecoveryCodeParameter], cancellationToken);

        context.Result = result.Status switch
        {
            SecondFactorStatus.OneProofRequired => new GrantValidationResult(TokenRequestErrors.InvalidRequest, OneCodeRequired),
            SecondFactorStatus.ChallengeInvalid => new GrantValidationResult(TokenRequestErrors.InvalidGrant, MfaTokenInvalid),
            SecondFactorStatus.AccountNotActive => new GrantValidationResult(TokenRequestErrors.InvalidGrant, HarmonyPasswordValidator.AccountNotActive),
            SecondFactorStatus.LockedOut => new GrantValidationResult(TokenRequestErrors.InvalidGrant, HarmonyPasswordValidator.LockedOut),
            SecondFactorStatus.CodeInvalid => new GrantValidationResult(TokenRequestErrors.InvalidGrant, OtpInvalid),

            // Same subject shape as the password grant; amr says how strongly the person signed in.
            _ => new GrantValidationResult(
                result.User!.Id.ToString("D"),
                "mfa",
                [new Claim(PlatformClaimTypes.SecurityVersion, result.User.SecurityVersion.ToString(CultureInfo.InvariantCulture))]),
        };
    }
}