using Duende.IdentityServer.Services;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The second step: the 6-digit code from the authenticator app, or one recovery code, checked by the same rules as
/// before (SecondFactorCheck, change 1.1). It needs the challenge the password page left in its cookie; without a live
/// one the person goes back to the password page.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
public sealed class TwoStepModel : PageModel
{
    private readonly IIdentityServerInteractionService interaction;
    private readonly SecondFactorCheck secondFactorCheck;

    public TwoStepModel(IIdentityServerInteractionService interaction, SecondFactorCheck secondFactorCheck)
    {
        this.interaction = interaction;
        this.secondFactorCheck = secondFactorCheck;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>False: the code from the app. True: a recovery code.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Recovery { get; set; }

    [BindProperty]
    public string? Code { get; set; }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted) is null)
        {
            return this.RedirectToPage("Error");
        }

        if (!await this.secondFactorCheck.IsLiveAsync(SignInSession.SecondStep(this.Request), this.HttpContext.RequestAborted))
        {
            return this.StartAgain();
        }

        return this.Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted) is null)
        {
            return this.RedirectToPage("Error");
        }

        var result = await this.secondFactorCheck.VerifyAsync(
            SignInSession.SecondStep(this.Request),
            this.Recovery ? null : this.Code,
            this.Recovery ? this.Code : null,
            this.HttpContext.RequestAborted);

        if (result.Status == SecondFactorStatus.Accepted)
        {
            SignInSession.ReleaseSecondStep(this.Response);
            await SignInSession.SignInAsync(this.HttpContext, result.User!, "mfa");
            return this.Redirect(this.ReturnUrl!);
        }

        if (result.Status == SecondFactorStatus.ChallengeInvalid)
        {
            return this.StartAgain();
        }

        this.Error = result.Status switch
        {
            SecondFactorStatus.OneProofRequired => "Enter the code from the app, or one recovery code.",
            SecondFactorStatus.AccountNotActive => "This account cannot sign in. Contact an administrator.",
            SecondFactorStatus.LockedOut => "Too many wrong attempts. The account is locked for a few minutes.",
            _ => "The code did not verify.",
        };
        return this.Page();
    }

    private IActionResult StartAgain()
    {
        SignInSession.ReleaseSecondStep(this.Response);
        return this.RedirectToPage("Login", new { this.ReturnUrl, notice = LoginModel.TimedOut });
    }
}
