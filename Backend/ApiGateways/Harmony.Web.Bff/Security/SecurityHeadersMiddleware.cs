namespace Harmony.Web.Bff.Security;

/// <summary>
/// Every answer leaves armored: content is never sniffed, never framed, never leaks a referrer, and the strict
/// default CSP suits the JSON this host speaks today (the page that serves the web app gets its own CSP when
/// static serving arrives). Headers are set up front, before any endpoint runs, so no path can forget them.
/// </summary>
internal sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate next;

    public SecurityHeadersMiddleware(RequestDelegate next) => this.next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        return this.next(context);
    }
}
