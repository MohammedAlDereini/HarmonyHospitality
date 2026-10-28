using Duende.IdentityServer.Services;
using Harmony.Core.Errors;
using Harmony.Identity.Handler.Commands.UserAccountModule.Password;
using Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Harmony.Identity.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The forced change at first sign-in: a password somebody else chose (a new account's initial password) is replaced
/// BEFORE the person is signed in. The page opens only on a sign-in the walk put on hold (the hold cookie, 10 minutes);
/// without one the person starts again. The change itself is the same sequence as the API's (lockout, current password,
/// Identity's rules); on success the held sign-in finishes exactly as it would have: Duende's cookie, the trail line, and
/// back to the sign-in request.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class ChangePasswordModel : PageModel
{
    private readonly IIdentityServerInteractionService interaction;
    private readonly ForcedPasswordChange forced;
    private readonly IMediator mediator;

    public ChangePasswordModel(IIdentityServerInteractionService interaction, ForcedPasswordChange forced, IMediator mediator)
    {
        this.interaction = interaction;
        this.forced = forced;
        this.mediator = mediator;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted) is null)
        {
            return this.RedirectToPage("Error");
        }

        if (await this.forced.FindAsync(SignInSession.PasswordChange(this.Request), this.HttpContext.RequestAborted) is null)
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

        var secret = SignInSession.PasswordChange(this.Request);
        var held = await this.forced.FindAsync(secret, this.HttpContext.RequestAborted);
        if (held is null)
        {
            return this.StartAgain();
        }

        if (string.IsNullOrEmpty(this.CurrentPassword) || string.IsNullOrEmpty(this.NewPassword))
        {
            this.Error = "Enter your current password and the new one.";
            return this.Page();
        }

        if (this.NewPassword != this.ConfirmPassword)
        {
            this.Error = "The two passwords do not match.";
            return this.Page();
        }

        var response = await this.mediator.Send(
            new FirstSignInPasswordChangeCommand { UserId = held.User.Id, CurrentPassword = this.CurrentPassword, NewPassword = this.NewPassword },
            this.HttpContext.RequestAborted);
        if (!response.IsSuccess)
        {
            this.Error = MessageFor(response.Errors.Select(error => error.Code));
            return this.Page();
        }

        // The hold is spent; the sign-in finishes as it would have, on the account as it is NOW (its version moved).
        await this.forced.ReleaseAsync(secret!, this.HttpContext.RequestAborted);
        SignInSession.ReleasePasswordChange(this.Response);
        var user = await this.forced.ReloadAsync(held.User.Id, this.HttpContext.RequestAborted);
        if (user is null)
        {
            return this.StartAgain();
        }

        if (await SignInSession.HoldForNewDeviceAsync(this, user, held.Method, held.TrailAction, this.ReturnUrl, held.Detail) is { } gate)
        {
            return gate;
        }

        var signedIn = await SignInSession.SignInAsync(this.HttpContext, user, held.Method.Split(','));
        await this.mediator.Send(
            new RecordSignInCommand { UserId = user.Id, TrailAction = held.TrailAction, DeviceKey = signedIn.DeviceKey, SessionId = signedIn.SessionId, Detail = held.Detail },
            this.HttpContext.RequestAborted);
        return this.Redirect(this.ReturnUrl!);
    }

    // The refusals a person can act on, in their words; anything else gets one honest line.
    private static string MessageFor(IEnumerable<string> codes)
    {
        foreach (var code in codes)
        {
            var message = code switch
            {
                BusinessErrorCodes.Identity.UserAccount.CurrentPasswordWrong => "The current password is wrong.",
                BusinessErrorCodes.Identity.UserAccount.PasswordRejected => "The password must be at least 12 characters and include a symbol, like - or ! (a space counts).",
                BusinessErrorCodes.Identity.UserAccount.NewPasswordSameAsCurrent => "The new password must be different from the current one.",
                BusinessErrorCodes.Identity.UserAccount.LockedOut => "Too many wrong attempts. The account is locked for a few minutes.",
                BusinessErrorCodes.Identity.UserAccount.PasswordLeaked => "This password appeared in a data leak. Choose a different one.",
                BusinessErrorCodes.Identity.UserAccount.PasswordHasContextWord => "Do not use \"Harmony\", your name or your e-mail name in the password.",
                _ => null,
            };
            if (message is not null)
            {
                return message;
            }
        }

        return "The password was not accepted.";
    }

    private IActionResult StartAgain()
    {
        SignInSession.ReleasePasswordChange(this.Response);
        return this.RedirectToPage("Login", new { this.ReturnUrl, notice = LoginModel.TimedOut });
    }
}
