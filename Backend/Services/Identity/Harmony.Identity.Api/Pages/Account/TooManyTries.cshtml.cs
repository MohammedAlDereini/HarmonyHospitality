using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// Item 5: where the rate limiter sends a refused sign-in send (Post/Redirect/Get, RateLimitPolicies.RefusalPage), so a
/// browser refresh never sends the refused form again. It answers 429 in the sign-in family's look and rules. Every wait
/// is a minute or less (one-minute buckets and steps), so the page can say so.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
public sealed class TooManyTriesModel : PageModel
{
    public void OnGet()
    {
        this.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    }
}
