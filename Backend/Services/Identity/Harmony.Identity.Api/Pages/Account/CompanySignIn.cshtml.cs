using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Handler.Commands.CompanyLoginModule.SignIn;
using Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;
using Harmony.Identity.Infrastructure.CompanyLogin;
using Harmony.Identity.Infrastructure.RateLimiting;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// Where a company sign-in lands (Duende's external-login callback). The provider's handler has already checked the token
/// and put the person in Duende's external cookie; this page reads it once, drops it, and asks CompanySignInCommand who
/// the person is in Harmony. Signed in: the session names the provider ("idp") and the method "external". Refused: back
/// to the sign-in page with a notice in plain words. Nothing renders here.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class CompanySignInModel : PageModel
{
    private readonly IIdentityServerInteractionService interaction;
    private readonly IMediator mediator;
    private readonly IOptions<PlatformTenantSettings> platformTenant;

    public CompanySignInModel(IIdentityServerInteractionService interaction, IMediator mediator, IOptions<PlatformTenantSettings> platformTenant)
    {
        this.interaction = interaction;
        this.mediator = mediator;
        this.platformTenant = platformTenant;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var external = await this.HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

        // The external cookie lives for this one step only, whatever happens next.
        await this.HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

        var returnUrl = Item(external.Properties, CompanyLoginSchemes.ReturnUrlItem) ?? this.ReturnUrl;
        var request = await this.interaction.GetAuthorizationContextAsync(returnUrl, this.HttpContext.RequestAborted);
        if (request is null)
        {
            return this.RedirectToPage("Error");
        }

        var schemeName = Item(external.Properties, CompanyLoginSchemes.SchemeItem);
        var scheme = CompanyLoginSchemes.SchemeOf(schemeName);
        var person = external.Principal;
        if (!external.Succeeded || person is null || scheme is null)
        {
            return this.Back(returnUrl!, "company-failed");
        }

        var issuer = person.FindFirst("iss")?.Value ?? person.FindFirst("sub")?.Issuer;
        var subject = person.FindFirst(scheme == AuthorityScheme.Microsoft ? "oid" : "sub")?.Value;
        if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(subject))
        {
            return this.Back(returnUrl!, "company-failed");
        }

        var email = person.FindFirst("email")?.Value;
        var decision = await this.mediator.Send(
            new CompanySignInCommand
            {
                Scheme = scheme.Value,
                Issuer = issuer,
                Subject = subject,
                Email = email,

                // Each provider says it itself: Microsoft's xms_edov (the e-mail's domain is verified by the company, an
                // optional claim set on Harmony's app), Google's email_verified. A missing claim is "not verified".
                EmailVerified = !string.IsNullOrEmpty(email) && IsTrue(person.FindFirst(scheme == AuthorityScheme.Microsoft ? "xms_edov" : "email_verified")?.Value),
                HostedDomain = person.FindFirst("hd")?.Value,
            },
            this.HttpContext.RequestAborted);

        if (decision.Account is not { } account)
        {
            return this.Back(returnUrl!, NoticeOf(decision.Refusal));
        }

        // A company sign-in is still a sign-in to this client: the console admits the platform's own people only.
        if (!PlatformOnly.Admits(request.Client, account, this.platformTenant.Value.TenantId))
        {
            await this.mediator.Send(new RecordSecurityEventCommand { UserId = account.Id, TrailAction = SecurityAuditActions.SignInPlatformOnly }, this.HttpContext.RequestAborted);
            return this.Back(returnUrl!, "company-platform-only");
        }

        // Two-step counts only when the company says it happened: Microsoft's amr (an optional claim) names "mfa". Google
        // says nothing about it, so a Google sign-in is never recorded as two-step.
        var companyTwoStep = scheme == AuthorityScheme.Microsoft && person.FindAll("amr").Any(c => c.Value == "mfa");
        var signedIn = await SignInSession.SignInWithCompanyAsync(this.HttpContext, account, schemeName!, companyTwoStep);
        await this.mediator.Send(
            new RecordSignInCommand { UserId = account.Id, TrailAction = SecurityAuditActions.SignInCompany, DeviceKey = signedIn.DeviceKey, SessionId = signedIn.SessionId, Detail = scheme.Value.ToString(), CompanyTwoStep = companyTwoStep },
            this.HttpContext.RequestAborted);
        return this.Redirect(returnUrl!);
    }

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string NoticeOf(string? refusal) => refusal switch
    {
        CompanySignInResult.NoAccount or CompanySignInResult.DomainNotListed or CompanySignInResult.NoVerifiedEmail => "company-no-account",
        CompanySignInResult.AccountNotActive => "company-not-active",
        CompanySignInResult.TenantNotActive => "company-tenant",
        _ => "company-refused",
    };

    private RedirectToPageResult Back(string returnUrl, string notice)
        => this.RedirectToPage("Login", new { ReturnUrl = returnUrl, notice });

    private static string? Item(AuthenticationProperties? properties, string key)
        => properties is not null && properties.Items.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;
}
