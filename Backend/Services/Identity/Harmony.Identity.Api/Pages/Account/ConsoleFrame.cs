using System.Reflection;
using Duende.IdentityServer.Services;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.IdentityServer;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Harmony.Identity.Infrastructure.IdentityServer.Yarp;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The console's sign-in frame (platform console design, C12): when the sign-in request comes from a platform-only client
/// (the console), the layout draws the brand panel beside the card, with the words the panel states. Every word is read,
/// not invented: the web app's address from the web BFF's origins, the version from this assembly, the two-step line from
/// the platform's own exception list, the contact and the links from <see cref="SignInPagesSettings"/>.
/// </summary>
/// <param name="WebAppUrl">Where hotel teams sign in: the web BFF's first origin.</param>
/// <param name="Version">This service's version, without the build hash.</param>
/// <param name="TwoStepExceptions">How many platform people are on the two-step exception list; 0 = two-step for everyone.</param>
/// <param name="NowUtc">When the page was drawn.</param>
public sealed record ConsoleFrame(
    string WebAppUrl,
    string Version,
    int TwoStepExceptions,
    string PlatformContact,
    string HelpUrl,
    string PrivacyUrl,
    string TermsUrl,
    DateTimeOffset NowUtc)
{
    private const string Key = "Harmony.SignInPage.ConsoleFrame";

    /// <summary>The frame the filter attached to this page, or null for the web app's plain card.</summary>
    public static ConsoleFrame? Of(ViewDataDictionary viewData) => viewData[Key] as ConsoleFrame;

    /// <summary>
    /// Attaches the frame to the page about to render when its sign-in request names a platform-only client. The return
    /// address comes with the request (query) or with the form (post); a page without one is drawn plain.
    /// </summary>
    public static async Task AttachAsync(ResultExecutingContext context)
    {
        if (context.Result is not PageResult page)
        {
            return;
        }

        var http = context.HttpContext;
        var returnUrl = http.Request.Query["ReturnUrl"].ToString();
        if (returnUrl.Length == 0 && http.Request.HasFormContentType)
        {
            returnUrl = http.Request.Form["ReturnUrl"].ToString();
        }

        if (returnUrl.Length == 0)
        {
            return;
        }

        var interaction = http.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
        var request = await interaction.GetAuthorizationContextAsync(returnUrl, http.RequestAborted);
        if (request is null || !IdentityServerResources.IsPlatformOnly(request.Client))
        {
            return;
        }

        var web = http.RequestServices.GetRequiredService<IOptions<WebBffSettings>>().Value;
        var words = http.RequestServices.GetRequiredService<IOptions<SignInPagesSettings>>().Value;
        var platformTenantId = http.RequestServices.GetRequiredService<IOptions<PlatformTenantSettings>>().Value.TenantId;
        var accounts = http.RequestServices.GetRequiredService<IUserAccountRepository>();
        var exceptions = await accounts.QueryAcrossTenants()
            .CountAsync(u => u.TenantId == platformTenantId && u.TwoStepExemptReason != null, http.RequestAborted);

        page.ViewData[Key] = new ConsoleFrame(
            web.Origins.FirstOrDefault() ?? string.Empty,
            VersionOf(typeof(ConsoleFrame).Assembly),
            exceptions,
            words.PlatformContact,
            words.HelpUrl,
            words.PrivacyUrl,
            words.TermsUrl,
            DateTimeOffset.UtcNow);
    }

    /// <summary>"1.2.3" from the informational version "1.2.3+abc123"; the assembly version when there is none.</summary>
    private static string VersionOf(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return assembly.GetName().Version?.ToString(3) ?? string.Empty;
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}
