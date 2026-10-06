using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.IdentityServer;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The sign-in page: e-mail and password, checked by the same rules as before (PasswordCheck, change 1.1).
/// It opens only for a real sign-in request from a known client (the returnUrl Duende gives); anything else goes to the
/// error page. With two-step sign-in on, the password earns a 5-minute challenge and the code page finishes the sign-in.
/// Two-step sign-in is required for everyone: without it, the password earns a setup link by e-mail, never a session.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
public sealed class LoginModel : PageModel
{
    public const string TimedOut = "timed-out";

    private readonly IIdentityServerInteractionService interaction;
    private readonly PasswordCheck passwordCheck;
    private readonly MfaChallengeStore challenges;
    private readonly TwoStepSetup setup;
    private readonly EmailSignIn emailSignIn;
    private readonly IAccountMailer mailer;
    private readonly IIssuerNameService issuer;

    public LoginModel(
        IIdentityServerInteractionService interaction,
        PasswordCheck passwordCheck,
        MfaChallengeStore challenges,
        TwoStepSetup setup,
        EmailSignIn emailSignIn,
        IAccountMailer mailer,
        IIssuerNameService issuer)
    {
        this.interaction = interaction;
        this.passwordCheck = passwordCheck;
        this.challenges = challenges;
        this.setup = setup;
        this.emailSignIn = emailSignIn;
        this.mailer = mailer;
        this.issuer = issuer;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? Error { get; private set; }

    public string? ForgotPasswordUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? notice)
    {
        var request = await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted);
        if (request is null)
        {
            return this.RedirectToPage("Error");
        }

        this.ForgotPasswordUrl = ForgotPasswordOf(request);
        this.Error = notice == TimedOut ? "The sign-in step timed out. Start again." : null;
        return this.Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted);
        if (request is null)
        {
            return this.RedirectToPage("Error");
        }

        var result = await this.passwordCheck.CheckAsync(this.Email, this.Password, tenant: null, this.HttpContext.RequestAborted);

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
            PasswordCheckStatus.LockedOut => "Too many wrong attempts. The account is locked for a few minutes.",
            PasswordCheckStatus.TenantRequired => "This e-mail exists in more than one company. Contact an administrator.",
            _ => "Wrong e-mail or password.",
        };
        return this.Page();
    }

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
