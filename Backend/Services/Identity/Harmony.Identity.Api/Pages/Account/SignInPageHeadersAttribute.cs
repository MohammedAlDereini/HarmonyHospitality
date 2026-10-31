using System.Security.Cryptography;
using Harmony.Identity.Infrastructure.CompanyLogin;
using Harmony.Identity.Infrastructure.IdentityServer.Yarp;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The rules every sign-in page is sent with. Script: only the one tag the layout writes, carrying this response's
/// nonce (a fresh random value each time), and Trusted Types on, so injected markup can never run code; that script may
/// call this host only (the passkey challenge). Styles only from this host; the forms post only here and their redirects
/// end only at the web app (the BFF) or, for a company e-mail, at the company's Microsoft or Google sign-in; no other site may frame the page, open it as a window it controls, or load it as a
/// resource; no camera, microphone or location; the browser keeps no copy, and the address never leaks to another site
/// in a Referer header.
/// </summary>
public sealed class SignInPageHeadersAttribute : ResultFilterAttribute
{
    private const string NonceKey = "Harmony.SignInPage.ScriptNonce";

    /// <summary>The nonce the layout puts on its script tag; empty outside a sign-in page, so no script runs there.</summary>
    public static string Nonce(HttpContext context)
        => context.Items[NonceKey] as string ?? string.Empty;

    /// <summary>A sign-in request from a platform-only client is drawn in the console's frame (C12).</summary>
    public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        await ConsoleFrame.AttachAsync(context);
        await base.OnResultExecutionAsync(context, next);
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is PageResult)
        {
            var bff = context.HttpContext.RequestServices.GetRequiredService<IOptions<WebBffSettings>>().Value;
            var company = context.HttpContext.RequestServices.GetRequiredService<IOptions<CompanyLoginSettings>>().Value;
            var headers = context.HttpContext.Response.Headers;

            // 128 random bits, new for every page: a guessed or replayed nonce never matches. Hex, so the HTML never escapes it.
            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            context.HttpContext.Items[NonceKey] = nonce;

            headers.ContentSecurityPolicy =
                $"default-src 'none'; script-src 'nonce-{nonce}'; style-src 'self'; img-src 'self'; connect-src 'self'; " +
                $"form-action 'self' {string.Join(' ', bff.Origins.Concat(company.SignInOrigins()))}; frame-ancestors 'none'; base-uri 'none'; " +
                "require-trusted-types-for 'script'";
            headers.XFrameOptions = "DENY";
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers.CacheControl = "no-store";
        }

        base.OnResultExecuting(context);
    }
}
