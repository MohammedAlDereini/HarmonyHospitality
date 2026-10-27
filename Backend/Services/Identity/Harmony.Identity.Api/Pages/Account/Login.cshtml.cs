using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;
using Harmony.Identity.Handler.Queries.CompanyLoginModule;
using Harmony.Identity.Infrastructure.CompanyLogin;
using Harmony.Identity.Infrastructure.IdentityServer;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Harmony.Identity.Infrastructure.RateLimiting;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The sign-in page: e-mail and password, checked by the same rules as before (PasswordCheck, change 1.1).
/// It opens only for a real sign-in request from a known client (the returnUrl Duende gives); anything else goes to the
/// error page. With two-step sign-in on, the password earns a 5-minute challenge and the code page finishes the sign-in.
/// Two-step sign-in is required for everyone: without it, the password earns a setup link by e-mail, never a session.
/// The one exception is the two-step exception list (like Mews): there the password alone signs the person in.
/// A passkey (item 6, like Mews) is the other way in: no password, no code; the key the person's device holds and the
/// face, fingerprint or PIN that unlocks it are the two factors, so it is a complete sign-in on its own.
/// Company login (stage A, the Mews way): an e-mail that a customer's switched-on company login covers goes to the
/// company's Microsoft or Google sign-in instead, before any password is checked; CompanySignIn finishes it.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class LoginModel : PageModel
{
    public const string TimedOut = "timed-out";

    /// <summary>The notices CompanySignIn and the company handlers send back here, and their words.</summary>
    public static readonly IReadOnlyDictionary<string, string> CompanyNotices = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["company-failed"] = "Your company's sign-in did not finish. Try again.",
        ["company-no-account"] = "Your company account is not set up in Harmony yet. Ask your administrator.",
        ["company-refused"] = "This company account cannot sign in to Harmony. Ask your administrator.",
        ["company-not-active"] = "This account cannot sign in. Contact an administrator.",
        ["company-tenant"] = TenantNotActiveMessage,
        ["company-platform-only"] = PlatformOnly.Message,
    };

    /// <summary>A company-login person tried a passkey: the company decides how they sign in.</summary>
    private const string CompanyLoginMessage = "Your company signs you in with its own account. Enter your work e-mail and press Sign in.";

    /// <summary>The company is suspended or terminated: the account is fine, so the words say the company, not the person.</summary>
    private const string TenantNotActiveMessage = "Your company's access to Harmony is suspended, so this account cannot sign in. Contact Harmony.";

    private readonly IIdentityServerInteractionService interaction;
    private readonly PasswordCheck passwordCheck;
    private readonly PasskeySignIn passkeySignIn;
    private readonly MfaChallengeStore challenges;
    private readonly TwoStepSetup setup;
    private readonly EmailSignIn emailSignIn;
    private readonly ForcedPasswordChange forced;
    private readonly IAccountMailer mailer;
    private readonly IIssuerNameService issuer;
    private readonly IMediator mediator;
    private readonly IOptions<IdentityPasskeyOptions> passkeyOptions;
    private readonly IOptions<PlatformTenantSettings> platformTenant;

    public LoginModel(
        IIdentityServerInteractionService interaction,
        PasswordCheck passwordCheck,
        PasskeySignIn passkeySignIn,
        MfaChallengeStore challenges,
        TwoStepSetup setup,
        EmailSignIn emailSignIn,
        ForcedPasswordChange forced,
        IAccountMailer mailer,
        IIssuerNameService issuer,
        IMediator mediator,
        IOptions<IdentityPasskeyOptions> passkeyOptions,
        IOptions<PlatformTenantSettings> platformTenant)
    {
        this.passkeyOptions = passkeyOptions;
        this.platformTenant = platformTenant;
        this.interaction = interaction;
        this.passwordCheck = passwordCheck;
        this.passkeySignIn = passkeySignIn;
        this.challenges = challenges;
        this.setup = setup;
        this.emailSignIn = emailSignIn;
        this.forced = forced;
        this.mailer = mailer;
        this.issuer = issuer;
        this.mediator = mediator;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    /// <summary>The browser's signed passkey answer (PublicKeyCredential.toJSON()), sent by the page's script.</summary>
    [BindProperty]
    public string? CredentialJson { get; set; }

    public string? Error { get; private set; }

    public string? ForgotPasswordUrl { get; private set; }

    /// <summary>
    /// Set when a refused answer named a passkey nobody holds any more (removed here, still on the device): the page's
    /// script hands it to the browser's signal method, so the device stops offering a dead key (WebAuthn Signal API).
    /// </summary>
    public string? UnknownCredentialId { get; private set; }

    /// <summary>The relying-party domain the signal names, the one every passkey here was made for.</summary>
    public string RpId => this.passkeyOptions.Value.ServerDomain!;

    public async Task<IActionResult> OnGetAsync(string? notice)
    {
        var request = await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted);
        if (request is null)
        {
            return this.RedirectToPage("Error");
        }

        // The device key is named with the page, before any send: both copies of a doubled send then carry the same key.
        SignInSession.EnsureDeviceKey(this.HttpContext);

        this.ForgotPasswordUrl = ForgotPasswordOf(request);
        this.Error = notice == TimedOut
            ? "The sign-in step timed out. Start again."
            : notice is not null && CompanyNotices.TryGetValue(notice, out var words) ? words : null;
        return this.Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted);
        if (request is null)
        {
            return this.RedirectToPage("Error");
        }

        // Company login first: an e-mail a switched-on company login covers goes to the company, and no password is checked.
        if (await this.mediator.Send(new GetCompanyRouteQuery { Email = this.Email }, this.HttpContext.RequestAborted) is { } route)
        {
            return this.Challenge(this.CompanyChallenge(route), route.AuthenticationScheme);
        }

        if (string.IsNullOrEmpty(this.Password))
        {
            this.ForgotPasswordUrl = ForgotPasswordOf(request);
            this.Error = "Enter your password.";
            return this.Page();
        }

        var result = await this.passwordCheck.CheckAsync(this.Email, this.Password, tenant: null, this.HttpContext.RequestAborted);

        // A refused sign-in of a known account goes in its trail; an unknown name has no account to write to.
        if (result.User is not null && RefusalOf(result.Status) is { } refusal)
        {
            await this.mediator.Send(new RecordSecurityEventCommand { UserId = result.User.Id, TrailAction = refusal }, this.HttpContext.RequestAborted);
        }

        // The password passed; is this client for this person? A platform-only client (the console) refuses a hotel
        // account here, before any challenge, link or session: shown on this page, never returned to the client.
        if (result.User is not null
            && result.Status is PasswordCheckStatus.Accepted or PasswordCheckStatus.SecondFactorRequired or PasswordCheckStatus.Exempt
            && !PlatformOnly.Admits(request.Client, result.User, this.platformTenant.Value.TenantId))
        {
            await this.mediator.Send(new RecordSecurityEventCommand { UserId = result.User.Id, TrailAction = SecurityAuditActions.SignInPlatformOnly }, this.HttpContext.RequestAborted);
            this.ForgotPasswordUrl = ForgotPasswordOf(request);
            this.Error = PlatformOnly.Message;
            return this.Page();
        }

        if (result.Status == PasswordCheckStatus.Exempt)
        {
            // A password somebody else chose: proved, not signed in — the change page finishes this sign-in.
            if (result.User!.MustChangePassword)
            {
                return await SignInSession.HoldForPasswordChangeAsync(this, this.forced, result.User, "pwd", SecurityAuditActions.SignInPasswordOnly, this.ReturnUrl);
            }

            // On the exception list: signed in with the password alone, and the session says so ("pwd").
            var signedIn = await SignInSession.SignInAsync(this.HttpContext, result.User!, "pwd");
            await this.mediator.Send(
                new RecordSignInCommand { UserId = result.User!.Id, TrailAction = SecurityAuditActions.SignInPasswordOnly, DeviceKey = signedIn.DeviceKey, SessionId = signedIn.SessionId },
                this.HttpContext.RequestAborted);
            return this.Redirect(this.ReturnUrl!);
        }

        if (result.Status == PasswordCheckStatus.Accepted)
        {
            // Right password, two-step not set up yet: nobody is signed in. A setup link goes to the account's e-mail and
            // works only in this browser.
            var (browser, link) = await this.setup.StartAsync(result.User!, this.HttpContext.RequestAborted);
            SignInSession.HoldSetup(this.Response, browser);
            await this.mailer.SendTwoStepSetupAsync(result.User!, await this.LinkAsync("TwoStepSetup", link), this.HttpContext.RequestAborted);
            return this.RedirectToPage("TwoStepSetup", new { this.ReturnUrl });
        }

        if (result.Status == PasswordCheckStatus.SecondFactorRequired)
        {
            // The pause before the second step; with it a backup code still works, whatever the method.
            SignInSession.HoldSecondStep(this.Response, await this.challenges.IssueAsync(result.User!.Id, this.HttpContext.RequestAborted));

            // E-mail people (like Mews): a one-time sign-in link to the account's e-mail, for this browser only.
            if (result.User.TwoStepMethod == TwoStepMethod.EmailLink)
            {
                var (browser, link) = await this.emailSignIn.StartAsync(result.User, this.HttpContext.RequestAborted);
                SignInSession.HoldEmailLink(this.Response, browser);
                await this.mailer.SendSignInLinkAsync(result.User, await this.LinkAsync("EmailSignIn", link), this.HttpContext.RequestAborted);
                return this.RedirectToPage("EmailSignIn", new { this.ReturnUrl });
            }

            return this.RedirectToPage("TwoStep", new { this.ReturnUrl });
        }

        this.ForgotPasswordUrl = ForgotPasswordOf(request);
        this.Error = result.Status switch
        {
            PasswordCheckStatus.AccountNotActive => "This account cannot sign in. Contact an administrator.",
            PasswordCheckStatus.TenantNotActive => TenantNotActiveMessage,
            PasswordCheckStatus.LockedOut => "Too many wrong attempts. The account is locked for a few minutes.",
            PasswordCheckStatus.TenantRequired => "This e-mail exists in more than one company. Contact an administrator.",
            _ => "Wrong e-mail or password.",
        };
        return this.Page();
    }

    /// <summary>
    /// The passkey challenge for the page's script (WebAuthn request options as JSON), held 5 minutes behind a cookie.
    /// A POST like the forms, so the anti-forgery token guards it; nothing of value is in the answer, but a challenge
    /// should never be handed to another site's page.
    /// </summary>
    public async Task<IActionResult> OnPostPasskeyOptionsAsync()
    {
        if (await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted) is null)
        {
            return this.BadRequest();
        }

        var (options, secret) = await this.passkeySignIn.StartAsync(this.HttpContext, this.HttpContext.RequestAborted);
        SignInSession.HoldPasskey(this.Response, secret);
        this.Response.Headers.CacheControl = "no-store";
        return this.Content(options, "application/json");
    }

    /// <summary>The browser's signed answer. Accepted, a passkey is a complete sign-in: "swk" and "mfa", like Mews.</summary>
    public async Task<IActionResult> OnPostPasskeyAsync()
    {
        var request = await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted);
        if (request is null)
        {
            return this.RedirectToPage("Error");
        }

        var result = await this.passkeySignIn.FinishAsync(SignInSession.Passkey(this.Request), this.CredentialJson, this.HttpContext, this.HttpContext.RequestAborted);
        SignInSession.ReleasePasskey(this.Response);

        var refusal = result.Status switch
        {
            PasskeySignInStatus.AccountNotActive => SecurityAuditActions.SignInNotActive,
            PasskeySignInStatus.TenantNotActive => SecurityAuditActions.SignInTenantNotActive,
            PasskeySignInStatus.LockedOut => SecurityAuditActions.SignInLockedOut,
            PasskeySignInStatus.Refused => SecurityAuditActions.SignInPasskeyRefused,
            _ => null,
        };
        if (result.User is not null && refusal is not null)
        {
            await this.mediator.Send(new RecordSecurityEventCommand { UserId = result.User.Id, TrailAction = refusal }, this.HttpContext.RequestAborted);
        }

        // The passkey verified, but the person's company signs them in: the company decides, not a key held here.
        if (result.User is not null
            && result.Status == PasskeySignInStatus.Accepted
            && await this.mediator.Send(new GetCompanyRouteQuery { Email = result.User.Email }, this.HttpContext.RequestAborted) is not null)
        {
            await this.mediator.Send(new RecordSecurityEventCommand { UserId = result.User.Id, TrailAction = SecurityAuditActions.SignInCompanyRefused }, this.HttpContext.RequestAborted);
            SignInSession.EnsureDeviceKey(this.HttpContext);
            this.ForgotPasswordUrl = ForgotPasswordOf(request);
            this.Error = CompanyLoginMessage;
            return this.Page();
        }

        // The passkey verified; is this client for this person? The same door as the password path.
        if (result.User is not null
            && result.Status == PasskeySignInStatus.Accepted
            && !PlatformOnly.Admits(request.Client, result.User, this.platformTenant.Value.TenantId))
        {
            await this.mediator.Send(new RecordSecurityEventCommand { UserId = result.User.Id, TrailAction = SecurityAuditActions.SignInPlatformOnly }, this.HttpContext.RequestAborted);
            SignInSession.EnsureDeviceKey(this.HttpContext);
            this.ForgotPasswordUrl = ForgotPasswordOf(request);
            this.Error = PlatformOnly.Message;
            return this.Page();
        }

        if (result.Status == PasskeySignInStatus.Accepted)
        {
            SignInSession.EnsureDeviceKey(this.HttpContext);

            // A password somebody else chose is still waiting to be replaced: proved, not signed in.
            if (result.User!.MustChangePassword)
            {
                return await SignInSession.HoldForPasswordChangeAsync(this, this.forced, result.User, string.Join(',', SignInSession.PasskeyMethods), SecurityAuditActions.SignInPasskey, this.ReturnUrl, result.PasskeyName);
            }

            // The trail line names the key that signed in (OWASP: record which credential was used), never the key itself.
            var signedIn = await SignInSession.SignInAsync(this.HttpContext, result.User!, SignInSession.PasskeyMethods);
            await this.mediator.Send(
                new RecordSignInCommand { UserId = result.User!.Id, TrailAction = SecurityAuditActions.SignInPasskey, DeviceKey = signedIn.DeviceKey, SessionId = signedIn.SessionId, Detail = result.PasskeyName },
                this.HttpContext.RequestAborted);
            return this.Redirect(this.ReturnUrl!);
        }

        SignInSession.EnsureDeviceKey(this.HttpContext);
        this.UnknownCredentialId = result.UnknownCredentialId;
        this.ForgotPasswordUrl = ForgotPasswordOf(request);
        this.Error = result.Status switch
        {
            PasskeySignInStatus.AccountNotActive => "This account cannot sign in. Contact an administrator.",
            PasskeySignInStatus.TenantNotActive => TenantNotActiveMessage,
            PasskeySignInStatus.LockedOut => "Too many wrong attempts. The account is locked for a few minutes.",
            PasskeySignInStatus.ChallengeInvalid => "The sign-in step timed out. Start again.",
            _ => "The passkey was not accepted. Try again, or sign in with your password.",
        };
        return this.Page();
    }

    // The company's sign-in, with where to come back and the hints that spare the person typing their e-mail again.
    private AuthenticationProperties CompanyChallenge(CompanyRoute route)
    {
        var properties = new AuthenticationProperties { RedirectUri = this.Url.Page("CompanySignIn", new { this.ReturnUrl }) };
        properties.Items[CompanyLoginSchemes.ReturnUrlItem] = this.ReturnUrl;
        properties.Items[CompanyLoginSchemes.SchemeItem] = route.AuthenticationScheme;
        properties.Items[CompanyLoginSchemes.LoginHintItem] = this.Email!.Trim();
        properties.Items[CompanyLoginSchemes.DomainHintItem] = route.Domain;
        return properties;
    }

    // The trail action for a refused password step, or null when the step was not refused.
    private static string? RefusalOf(PasswordCheckStatus status) => status switch
    {
        PasswordCheckStatus.InvalidCredentials => SecurityAuditActions.SignInWrongPassword,
        PasswordCheckStatus.LockedOut => SecurityAuditActions.SignInLockedOut,
        PasswordCheckStatus.AccountNotActive => SecurityAuditActions.SignInNotActive,
        PasswordCheckStatus.TenantNotActive => SecurityAuditActions.SignInTenantNotActive,
        _ => null,
    };

    // The web app's own "forgot password" page, on the address the sign-in will return to.
    private static string ForgotPasswordOf(AuthorizationRequest request)
        => new Uri(request.RedirectUri).GetLeftPart(UriPartial.Authority) + "/forgot-password";

    // A link for an e-mail, built on Identity's configured address, never on the request's Host header.
    private async Task<string> LinkAsync(string page, string token)
    {
        var path = this.Url.Page(page, pageHandler: null, values: new { token, this.ReturnUrl })!;
        var address = await this.issuer.GetCurrentAsync(this.HttpContext.RequestAborted);
        return address.TrimEnd('/') + path;
    }
}
