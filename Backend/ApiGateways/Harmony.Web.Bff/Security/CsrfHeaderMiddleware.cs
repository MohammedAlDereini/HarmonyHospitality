namespace Harmony.Web.Bff.Security;

/// <summary>
/// The CSRF wall (RFC 10017 §6.1.3.3, the custom-header scheme; the header name and value are Duende BFF's own
/// convention): every /bff and /api request must carry "x-csrf: 1". A browser only adds custom headers to
/// fetch() calls from scripts, and a CROSS-site script cannot add them without a CORS preflight that nobody
/// answers — so a forged form or image tag dies here, cookie or no cookie. The sign-in walk is exempt: those
/// are top-level navigations, which can never carry custom headers, and each is protected by its own state,
/// nonce or sid proof instead. When an Origin header is present it must be OUR OWN origin — a second, free wall.
/// </summary>
internal sealed class CsrfHeaderMiddleware
{
    internal const string HeaderName = "x-csrf";
    internal const string HeaderValue = "1";

    /// <summary>
    /// Top-level navigations of the sign-in/sign-out walk (no fetch(), so no custom header possible): sign-in,
    /// the callback, sign-out (guarded by the session's own sid in the link instead) and its callbacks; plus
    /// the back-channel receiver: Identity's own server POST, guarded by its signed logout_token instead; plus the CSP
    /// report door: the browser posts reports without custom headers, and the door takes only report content types,
    /// which no form can send.
    /// </summary>
    private static readonly string[] ExemptPaths =
    [
        "/bff/login",
        "/bff/logout",
        "/signin-oidc",
        "/signout-callback-oidc",
        "/signout-oidc",
        "/bff/backchannel-logout",
        CspViolationReports.Path,
    ];

    private readonly RequestDelegate next;

    public CsrfHeaderMiddleware(RequestDelegate next) => this.next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var guarded = (path.StartsWithSegments("/bff") || path.StartsWithSegments("/api"))
            && !ExemptPaths.Any(exempt => path.StartsWithSegments(exempt));

        if (guarded)
        {
            if (context.Request.Headers[HeaderName] != HeaderValue)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            // Belt on top: a browser names the calling site; when it does, only our own site may speak.
            var origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && !string.Equals(origin, $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        }

        return this.next(context);
    }
}
