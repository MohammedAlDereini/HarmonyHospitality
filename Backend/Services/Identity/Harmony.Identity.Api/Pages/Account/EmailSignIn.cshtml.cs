using Duende.IdentityServer.Services;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The second step for people who chose the e-mail link (like Mews). Without a token: "check your e-mail". With the token,
/// in the browser that typed the password: signed in, back to the sign-in request. Anywhere else the link does nothing.
/// A GET signs in here on purpose: the link only works together with this browser's cookie, so nobody can plant it.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
public sealed class EmailSignInModel : PageModel
{
    public enum Steps
    {
        CheckEmail,
        SignedIn,
        WrongBrowser,
        Expired,
    }

    private readonly IIdentityServerInteractionService interaction;
    private readonly EmailSignIn emailSignIn;

    public EmailSignInModel(IIdentityServerInteractionService interaction, EmailSignIn emailSignIn)
    {
        this.interaction = interaction;
        this.emailSignIn = emailSignIn;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public Steps Step { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? token)
    {
        if (token is null)
        {
            this.Step = Steps.CheckEmail;
            return this.Page();
        }

        var browser = SignInSession.EmailLink(this.Request);
        if (browser is null)
        {
            this.Step = Steps.WrongBrowser;
            return this.Page();
        }

        var user = await this.emailSignIn.CompleteAsync(browser, token, this.HttpContext.RequestAborted);
        if (user is null)
        {
            this.Step = Steps.Expired;
            return this.Page();
        }

        SignInSession.ReleaseEmailLink(this.Response);
        SignInSession.ReleaseSecondStep(this.Response);
        await SignInSession.SignInAsync(this.HttpContext, user, "mfa");

        // Back to the sign-in request while Duende still knows it; otherwise the person is signed in here and goes back.
        if (await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted) is not null)
        {
            return this.Redirect(this.ReturnUrl!);
        }

        this.Step = Steps.SignedIn;
        return this.Page();
    }
}
