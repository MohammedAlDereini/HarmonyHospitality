using Duende.IdentityServer.Services;
using Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Harmony.Identity.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The waiting room of the Mews gate (L16): the person proved who they are from a browser the account has not trusted.
/// The page says a mail went out, asks again every few seconds, and finishes the sign-in the moment the browser is
/// trusted. Nobody is signed in while it waits. A hold that is gone sends the person back to the start.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class NewDeviceModel : PageModel
{
    private readonly IIdentityServerInteractionService interaction;
    private readonly NewDeviceApproval approvals;
    private readonly IMediator mediator;

    public NewDeviceModel(IIdentityServerInteractionService interaction, NewDeviceApproval approvals, IMediator mediator)
    {
        this.interaction = interaction;
        this.approvals = approvals;
        this.mediator = mediator;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Where the mail went, for the person to look.</summary>
    public string? Email { get; private set; }

    public bool Resent { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted) is null)
        {
            return this.RedirectToPage("Error");
        }

        var held = await this.approvals.FindAsync(SignInSession.NewDevice(this.Request), this.HttpContext.RequestAborted);
        if (held is null)
        {
            return this.StartAgain();
        }

        if (!await this.approvals.IsTrustedAsync(held, this.HttpContext.RequestAborted))
        {
            this.Email = held.User.Email;
            return this.Page();
        }

        // Trusted now: the sign-in the person already proved finishes here, recorded the way its own page would have.
        await this.approvals.ReleaseAsync(SignInSession.NewDevice(this.Request)!, this.HttpContext.RequestAborted);
        SignInSession.ReleaseNewDevice(this.Response);
        var signedIn = await SignInSession.SignInAsync(this.HttpContext, held.User, held.Method.Split(','));
        await this.mediator.Send(
            new RecordSignInCommand { UserId = held.User.Id, TrailAction = held.TrailAction, DeviceKey = signedIn.DeviceKey, SessionId = signedIn.SessionId, Detail = held.Detail },
            this.HttpContext.RequestAborted);
        return this.Redirect(this.ReturnUrl!);
    }

    /// <summary>The first mail is late or lost: a new link goes out for the same held sign-in.</summary>
    public async Task<IActionResult> OnPostResendAsync()
    {
        if (await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted) is null)
        {
            return this.RedirectToPage("Error");
        }

        var held = await this.approvals.FindAsync(SignInSession.NewDevice(this.Request), this.HttpContext.RequestAborted);
        if (held is null)
        {
            return this.StartAgain();
        }

        await this.approvals.ResendAsync(held, this.HttpContext.RequestAborted);
        this.Email = held.User.Email;
        this.Resent = true;
        return this.Page();
    }

    private IActionResult StartAgain()
    {
        SignInSession.ReleaseNewDevice(this.Response);
        return this.RedirectToPage("Login", new { this.ReturnUrl, notice = LoginModel.TimedOut });
    }
}
