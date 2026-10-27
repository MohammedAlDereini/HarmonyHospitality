namespace Harmony.Identity.Handler.Commands.CompanyLoginModule.SignIn;

using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// A person came back from the company's Microsoft or Google sign-in, the token already checked by the handler (signature,
/// audience, lifetime, nonce, Microsoft's issuer rules). Which customer and which account they are is decided here, from
/// Harmony's own tables. Sent by the sign-in page, never by a client.
/// </summary>
public class CompanySignInCommand : BaseCommandRequest<CompanySignInResult>
{
    public AuthorityScheme Scheme { get; set; }

    /// <summary>The token's iss.</summary>
    public string Issuer { get; set; } = null!;

    /// <summary>The provider's permanent id for the person: Microsoft's oid, Google's sub.</summary>
    public string Subject { get; set; } = null!;

    public string? Email { get; set; }

    /// <summary>Google's email_verified. Microsoft gives an e-mail to a multi-tenant app only when the company verified its domain.</summary>
    public bool EmailVerified { get; set; }

    /// <summary>Google's hd: the Workspace domain. Null for Microsoft.</summary>
    public string? HostedDomain { get; set; }
}

/// <summary>The account to sign in, or why not (a refusal word for the log and the page; the trail has it too).</summary>
public sealed record CompanySignInResult(User? Account, string? Refusal)
{
    public const string CompanyUnknown = "company-unknown";
    public const string CompanyLoginOff = "company-login-off";
    public const string NoVerifiedEmail = "no-verified-email";
    public const string DomainNotListed = "domain-not-listed";
    public const string NoAccount = "no-account";
    public const string LinkEnded = "link-ended";
    public const string LinkedToAnother = "linked-to-another";
    public const string AccountNotActive = "account-not-active";
    public const string TenantNotActive = "tenant-not-active";
    public const string PasswordSignIn = "password-sign-in";

    public bool SignedIn => this.Account is not null;

    public static CompanySignInResult In(User account) => new(account, null);

    public static CompanySignInResult Refused(string why) => new(null, why);
}
