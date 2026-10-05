using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// Signing out of Identity. Duende sends the browser here from its end-session endpoint. When the request comes from a
/// known app with a valid id_token_hint for this session, the person is signed out at once and sent back to the app;
/// otherwise the page asks first (a form, so a link from another site cannot sign anyone out).
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
public sealed class LogoutModel : PageModel
{
    private readonly IIdentityServerInteractionService interaction;
    private readonly IUserSession userSession;

    public LogoutModel(IIdentityServerInteractionService interaction, IUserSession userSession)
    {
        this.interaction = interaction;
        this.userSession = userSession;
    }

    [BindProperty(SupportsGet = true)]
    public string? LogoutId { get; set; }

    public bool SignedOut { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var logout = await this.interaction.GetLogoutContextAsync(this.LogoutId, this.HttpContext.RequestAborted);
        var signedIn = await this.userSession.GetUserAsync(this.HttpContext.RequestAborted) is not null;

        return signedIn && logout.ShowSignoutPrompt ? this.Page() : await this.SignOutAsync(logout);
    }

    public async Task<IActionResult> OnPostAsync()
        => await this.SignOutAsync(await this.interaction.GetLogoutContextAsync(this.LogoutId, this.HttpContext.RequestAborted));

    private async Task<IActionResult> SignOutAsync(LogoutRequest logout)
    {
        if (await this.userSession.GetUserAsync(this.HttpContext.RequestAborted) is not null)
        {
            // Duende's cookie by name: the default scheme here is the platform token, which has no sign-out.
            await this.HttpContext.SignOutAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);
        }

        // Duende checked this address against the app's registered sign-out addresses.
        if (!string.IsNullOrEmpty(logout.PostLogoutRedirectUri))
        {
            return this.Redirect(logout.PostLogoutRedirectUri);
        }

        this.SignedOut = true;
        return this.Page();
    }
}
