namespace Harmony.Web.Bff.Security;

/// <summary>
/// Every answer leaves armored: content is never sniffed, never framed, never leaks a referrer. Two CSPs: the
/// JSON doors (/bff, /api, /.well-known) allow NOTHING, and the web app's own page allows only itself — its scripts and styles
/// from this origin, no inline script, no foreign origin at all. Headers are set up front, before any endpoint
/// runs, so no path can forget them.
/// </summary>
internal sealed class SecurityHeadersMiddleware
{
    private const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
    private const string PagePolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

    private readonly RequestDelegate next;

    public SecurityHeadersMiddleware(RequestDelegate next) => this.next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers.ContentSecurityPolicy = path.StartsWithSegments("/bff") || path.StartsWithSegments("/api") || path.StartsWithSegments("/.well-known") ? ApiPolicy : PagePolicy;
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        return this.next(context);
    }
}
