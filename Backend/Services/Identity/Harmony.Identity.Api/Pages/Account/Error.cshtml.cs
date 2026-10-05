using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// Where Duende sends the browser when a sign-in request is wrong (unknown client, bad address, expired request).
/// The person sees a plain message and a reference; the technical reason is shown only in Development.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
public sealed class ErrorModel : PageModel
{
    private readonly IIdentityServerInteractionService interaction;
    private readonly IWebHostEnvironment environment;

    public ErrorModel(IIdentityServerInteractionService interaction, IWebHostEnvironment environment)
    {
        this.interaction = interaction;
        this.environment = environment;
    }

    public string Reference { get; private set; } = string.Empty;

    public string? Detail { get; private set; }

    public async Task OnGetAsync(string? errorId)
    {
        var error = await this.interaction.GetErrorContextAsync(errorId, this.HttpContext.RequestAborted);
        this.Reference = error?.RequestId ?? this.HttpContext.TraceIdentifier;

        if (error is not null && this.environment.IsDevelopment())
        {
            this.Detail = $"{error.Error}: {error.ErrorDescription}";
        }
    }
}
