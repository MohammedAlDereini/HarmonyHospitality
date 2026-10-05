using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Harmony.Identity.Infrastructure.IdentityServer;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The sign-in page: e-mail and password, checked by the same rules as before (PasswordCheck, change 1.1).
/// It opens only for a real sign-in request from a known client (the returnUrl Duende gives); anything else goes to the
/// error page. With two-step sign-in on, the password earns a 5-minute challenge and the code page finishes the sign-in.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
public sealed class LoginModel : PageModel
{
    public const string TimedOut = "timed-out";

    private readonly IIdentityServerInteractionService interaction;
    private readonly PasswordCheck passwordCheck;
    private readonly MfaChallengeStore challenges;

    public LoginModel(IIdentityServerInteractionService interaction, PasswordCheck passwordCheck, MfaChallengeStore challenges)
    {
        this.interaction = interaction;
        this.passwordCheck = passwordCheck;
        this.challenges = challenges;
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
            await SignInSession.SignInAsync(this.HttpContext, result.User!, "pwd");
            return this.Redirect(this.ReturnUrl!);
        }

        if (result.Status == PasswordCheckStatus.SecondFactorRequired)
        {
            SignInSession.HoldSecondStep(this.Response, await this.challenges.IssueAsync(result.User!.Id, this.HttpContext.RequestAborted));
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
}
