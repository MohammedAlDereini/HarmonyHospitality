namespace Harmony.Web.Bff.Security;

using Harmony.Web.Bff.Settings;
using Microsoft.Extensions.Options;

/// <summary>
/// Every answer leaves armored: content is never sniffed, never framed, never leaks a referrer. Three CSPs: the JSON
/// doors (/bff, /api, /.well-known) allow NOTHING; the docs page allows itself; the web app's page allows itself plus
/// the inline scripts and styles that carry THIS request's nonce (Angular stamps the nonce it is given on every style
/// it inserts), no foreign origin at all. Trusted Types ride on the page policy: enforced when the setting says so,
/// report-only until then (the staging week), so a violation is seen before it ever blocks. The headers are registered
/// up front (no path can forget them) and written when the response starts: after the proxy copied the API's own
/// headers, so a proxied answer carries each header once, with the proxy's value. Every /api answer is no-store.
/// </summary>
internal sealed class SecurityHeadersMiddleware
{
    private const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
    // Swagger UI (the /docs page) writes style attributes from script; everything else stays as tight as the pages.
    private const string DocsPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    private const string TrustedTypes = "require-trusted-types-for 'script'; trusted-types angular angular#bundler";
    // Both report directives: report-to for today's browsers, report-uri for the older ones (Microsoft: keep both for now).
    private const string Reporting = $"report-to csp; report-uri {CspViolationReports.Path}";
    private const string ReportingEndpoints = $"csp=\"{CspViolationReports.Path}\"";

    private readonly RequestDelegate next;
    private readonly bool enforceTrustedTypes;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<BffSettings> settings)
    {
        this.next = next;
        this.enforceTrustedTypes = settings.Value.EnforceTrustedTypes;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var door = path.StartsWithSegments("/bff") || path.StartsWithSegments("/api") || path.StartsWithSegments("/.well-known");
        var docs = path.StartsWithSegments("/docs");

        // The nonce is issued now, before the page renders; the headers are written when the response starts, after
        // anything the proxy copied from the API, so the proxy's value is the one value (never two, never the API's).
        var nonce = door || docs ? null : CspNonce.Issue(context);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            if (door)
            {
                headers.ContentSecurityPolicy = ApiPolicy;
                headers.Remove("Content-Security-Policy-Report-Only");
            }
            else if (docs)
            {
                headers.ContentSecurityPolicy = DocsPolicy;
            }
            else
            {
                var page = PagePolicy(nonce!);
                headers["Reporting-Endpoints"] = ReportingEndpoints;
                if (this.enforceTrustedTypes)
                {
                    headers.ContentSecurityPolicy = $"{page}; {TrustedTypes}; {Reporting}";
                }
                else
                {
                    headers.ContentSecurityPolicy = $"{page}; {Reporting}";
                    headers.ContentSecurityPolicyReportOnly = $"{TrustedTypes}; {Reporting}";
                }
            }

            // A browser session's API answers are per person: never stored by the browser or anything in between.
            if (path.StartsWithSegments("/api"))
            {
                headers.CacheControl = "no-store";
            }

            // Browser features: the three nobody at a hotel desk needs are denied; the two passkey features stay at their
            // default (self), named so a longer deny list can never take them by mistake. Chrome and Edge honour the
            // header; Firefox and Safari do not read it (caniuse, Oct 2026), so a long list buys nothing there.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), publickey-credentials-create=(self), publickey-credentials-get=(self)";
            // Flash, Acrobat and Silverlight readers never pull our data cross-domain (deprecated clients; scanners still check).
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            return Task.CompletedTask;
        });

        return this.next(context);
    }

    private static string PagePolicy(string nonce)
        => $"default-src 'self'; script-src 'self' 'nonce-{nonce}'; style-src 'self' 'nonce-{nonce}'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
}
