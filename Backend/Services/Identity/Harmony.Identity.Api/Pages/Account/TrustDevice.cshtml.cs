using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Harmony.Identity.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The "Trust device" link from the e-mail (L16): one open marks the browser that is waiting as trusted and spends the
/// link; the waiting tab then finishes the sign-in by itself. Opened from any device: it is the mailbox that proves
/// the person, not this browser.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class TrustDeviceModel : PageModel
{
    private readonly NewDeviceApproval approvals;

    public TrustDeviceModel(NewDeviceApproval approvals)
    {
        this.approvals = approvals;
    }

    /// <summary>The browser that is trusted now, or null when the link was not good.</summary>
    public string? Device { get; private set; }

    public async Task OnGetAsync(string? token)
    {
        this.Device = (await this.approvals.TrustAsync(token, this.HttpContext.RequestAborted))?.Label;
    }
}
