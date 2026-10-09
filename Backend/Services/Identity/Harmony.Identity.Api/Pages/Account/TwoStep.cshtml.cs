using Duende.IdentityServer.Services;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Harmony.Identity.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The second step: the 6-digit code from the authenticator app, or one recovery code, checked by the same rules as
/// before (SecondFactorCheck, change 1.1). It needs the challenge the password page left in its cookie; without a live
/// one the person goes back to the password page.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class TwoStepModel : PageModel
{
    private readonly IIdentityServerInteractionService interaction;
    private readonly SecondFactorCheck secondFactorCheck;
    private readonly IMediator mediator;

    public TwoStepModel(IIdentityServerInteractionService interaction, SecondFactorCheck secondFactorCheck, IMediator mediator)
    {
        this.interaction = interaction;
        this.secondFactorCheck = secondFactorCheck;
        this.mediator = mediator;
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

        // In the trail: a wrong code or a lock-out of a known account, and a backup code used (an account recovery).
        var trail = result.Status switch
        {
            SecondFactorStatus.CodeInvalid => SecurityAuditActions.SignInWrongCode,
            SecondFactorStatus.LockedOut => SecurityAuditActions.SignInLockedOut,
            SecondFactorStatus.Accepted when this.Recovery => SecurityAuditActions.BackupCodeUsed,
            _ => null,
        };
        if (result.User is not null && trail is not null)
        {
            await this.mediator.Send(new RecordSecurityEventCommand { UserId = result.User.Id, TrailAction = trail }, this.HttpContext.RequestAborted);
        }

        if (result.Status == SecondFactorStatus.Accepted)
        {
            SignInSession.ReleaseSecondStep(this.Response);
            var signedIn = await SignInSession.SignInAsync(this.HttpContext, result.User!, "mfa");
            await this.mediator.Send(
                new RecordSignInCommand { UserId = result.User!.Id, TrailAction = SecurityAuditActions.SignInTwoStep, DeviceKey = signedIn.DeviceKey, SessionId = signedIn.SessionId },
                this.HttpContext.RequestAborted);
            return this.Redirect(this.ReturnUrl!);
        }

        if (result.Status == SecondFactorStatus.AlreadyAccepted)
        {
            // The doubled send of a sign-in this browser just finished: walk it through, and record nothing twice.
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
