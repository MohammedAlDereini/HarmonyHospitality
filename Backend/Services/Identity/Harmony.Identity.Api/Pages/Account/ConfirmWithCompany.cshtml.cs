using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Handler.Commands.UserAccountModule.SignInRecords;
using Harmony.Identity.Handler.Queries.CompanyLoginModule;
using Harmony.Identity.Infrastructure.CompanyLogin;
using Harmony.Identity.Infrastructure.IdentityServer.Yarp;
using Harmony.Identity.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// "Confirm it is you" for a company person (stage B): they have no Harmony password or passkey, so the web app sends them
/// here and they sign in again at their company, forced (prompt=login, max_age=0). CompanyConfirmed checks the company
/// identity is theirs and fresh, records the proof, and sends them back to the web app.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class ConfirmWithCompanyModel : PageModel
{
    /// <summary>The person asking, kept in the external cookie's properties for the callback.</summary>
    public const string PersonItem = "confirm-person";

    /// <summary>Where to go back to (a web app address), kept the same way.</summary>
    public const string BackItem = "confirm-back";

    private readonly IUserSession userSession;
    private readonly IMediator mediator;
    private readonly IOptions<WebBffSettings> web;
    private readonly IOptions<ConsoleBffSettings> console;

    public ConfirmWithCompanyModel(IUserSession userSession, IMediator mediator, IOptions<WebBffSettings> web, IOptions<ConsoleBffSettings> console)
    {
        this.userSession = userSession;
        this.mediator = mediator;
        this.web = web;
        this.console = console;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Problem { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAppAddress(this.ReturnUrl, this.web.Value, this.console.Value))
        {
            return this.RedirectToPage("Error");
        }

        var person = await this.userSession.GetUserAsync(this.HttpContext.RequestAborted);
        if (!Guid.TryParse(person?.FindFirst("sub")?.Value, out var userId))
        {
            this.Problem = "You are not signed in here any more. Sign in again, then try once more.";
            return this.Page();
        }

        var route = await this.mediator.Send(new GetCompanyConfirmRouteQuery { UserId = userId }, this.HttpContext.RequestAborted);
        if (route is null)
        {
            this.Problem = "Your account confirms with its password or a passkey, not with a company sign-in.";
            return this.Page();
        }

        var properties = new OpenIdConnectChallengeProperties
        {
            RedirectUri = this.Url.Page("CompanyConfirmed"),
            Prompt = "login",
            MaxAge = TimeSpan.Zero,
        };
        properties.Items[CompanyLoginSchemes.SchemeItem] = route.Route.AuthenticationScheme;
        properties.Items[CompanyLoginSchemes.LoginHintItem] = route.Email;
        properties.Items[CompanyLoginSchemes.DomainHintItem] = route.Route.Domain;
        properties.Items[PersonItem] = userId.ToString("D");
        properties.Items[BackItem] = this.ReturnUrl;
        return this.Challenge(properties, route.Route.AuthenticationScheme);
    }

    /// <summary>An absolute address on one of the web app hosts (the BFF origins), never anywhere else.</summary>
    internal static bool IsAppAddress(string? url, WebBffSettings web, ConsoleBffSettings console)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps
           && web.Origins.Concat(console.Origins).Any(origin => string.Equals(origin.TrimEnd('/'), uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Where the forced company sign-in lands: the proof is recorded when the identity is the person's own and fresh.</summary>
[AllowAnonymous]
[SignInPageHeaders]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class CompanyConfirmedModel : PageModel
{
    private readonly IUserSession userSession;
    private readonly IMediator mediator;
    private readonly IOptions<WebBffSettings> web;
    private readonly IOptions<ConsoleBffSettings> console;

    public CompanyConfirmedModel(IUserSession userSession, IMediator mediator, IOptions<WebBffSettings> web, IOptions<ConsoleBffSettings> console)
    {
        this.userSession = userSession;
        this.mediator = mediator;
        this.web = web;
        this.console = console;
    }

    public string? Problem { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var external = await this.HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
        await this.HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

        var back = Item(external.Properties, ConfirmWithCompanyModel.BackItem);
        var scheme = CompanyLoginSchemes.SchemeOf(Item(external.Properties, CompanyLoginSchemes.SchemeItem));
        var asked = Item(external.Properties, ConfirmWithCompanyModel.PersonItem);
        var person = await this.userSession.GetUserAsync(this.HttpContext.RequestAborted);
        var signedIn = person?.FindFirst("sub")?.Value;
        var claims = external.Principal;
        if (!external.Succeeded || claims is null || scheme is null || !ConfirmWithCompanyModel.IsAppAddress(back, this.web.Value, this.console.Value)
            || asked is null || !string.Equals(asked, signedIn, StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(asked, out var userId))
        {
            this.Problem = "The confirmation did not finish. Go back to Harmony and try again.";
            return this.Page();
        }

        // auth_time when the company sends it; otherwise iat: the token answers this very request (its nonce), so it is fresh.
        var at = claims.FindFirst("auth_time")?.Value ?? claims.FindFirst("iat")?.Value;
        var authTime = long.TryParse(at, out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : (DateTimeOffset?)null;
        var result = await this.mediator.Send(
            new ConfirmWithCompanyCommand
            {
                UserId = userId,
                Scheme = scheme.Value,
                Issuer = claims.FindFirst("iss")?.Value ?? claims.FindFirst("sub")?.Issuer ?? string.Empty,
                Subject = claims.FindFirst(scheme == AuthorityScheme.Microsoft ? "oid" : "sub")?.Value ?? string.Empty,
                HostedDomain = claims.FindFirst("hd")?.Value,
                AuthenticatedAt = authTime,
            },
            this.HttpContext.RequestAborted);

        if (result.Status != Harmony.Core.Enums.eCallResponseStatus.Success)
        {
            this.Problem = "That company account is not the one linked to you, or the sign-in was not fresh. Try again.";
            return this.Page();
        }

        return this.Redirect(back!);
    }

    private static string? Item(AuthenticationProperties? properties, string key)
        => properties is not null && properties.Items.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;
}
