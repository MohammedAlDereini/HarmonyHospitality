using System.Globalization;
using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// The password grant (ROPC) the current web app signs in with: /connect/token with username and password. The rules
/// themselves live in <see cref="PasswordCheck"/>, shared with the sign-in page that replaces this grant (BFF, code + PKCE);
/// this class only turns their outcome into Duende's answer. To be removed once the web app signs in through the BFF.
/// </summary>
public sealed class HarmonyPasswordValidator : IResourceOwnerPasswordValidator
{
    public const string TenantParameter = "tenant_id";
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountNotActive = "account_not_active";
    public const string LockedOut = "locked_out";
    public const string TenantRequired = "tenant_required";
    public const string MfaRequired = "mfa_required";
    public const string MfaTokenField = "mfa_token";

    private readonly PasswordCheck passwordCheck;
    private readonly MfaChallengeStore challenges;

    public HarmonyPasswordValidator(PasswordCheck passwordCheck, MfaChallengeStore challenges)
    {
        this.passwordCheck = passwordCheck;
        this.challenges = challenges;
    }

    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context, CancellationToken cancellationToken)
    {
        var result = await this.passwordCheck.CheckAsync(context.UserName, context.Password, context.Request?.Raw?[TenantParameter], cancellationToken);

        context.Result = result.Status switch
        {
            PasswordCheckStatus.TenantRequired => new GrantValidationResult(TokenRequestErrors.InvalidRequest, TenantRequired),
            PasswordCheckStatus.AccountNotActive => new GrantValidationResult(TokenRequestErrors.InvalidGrant, AccountNotActive),
            PasswordCheckStatus.LockedOut => new GrantValidationResult(TokenRequestErrors.InvalidGrant, LockedOut),

            // Two-factor on: the password alone earns a short-lived challenge, not a token. The mfa_otp grant finishes the sign-in.
            PasswordCheckStatus.SecondFactorRequired => new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                MfaRequired,
                new Dictionary<string, object> { [MfaTokenField] = await this.challenges.IssueAsync(result.User!.Id, cancellationToken) }),

            // The token claims come from HarmonyProfileService. The subject keeps one thing of its own: the SecurityVersion
            // it signed in with, stored inside the refresh token, so a later bump ends that session at the next refresh.
            PasswordCheckStatus.Accepted => new GrantValidationResult(
                result.User!.Id.ToString("D"),
                "pwd",
                [new Claim(PlatformClaimTypes.SecurityVersion, result.User.SecurityVersion.ToString(CultureInfo.InvariantCulture))]),

            _ => new GrantValidationResult(TokenRequestErrors.InvalidGrant, InvalidCredentials),
        };
    }
}