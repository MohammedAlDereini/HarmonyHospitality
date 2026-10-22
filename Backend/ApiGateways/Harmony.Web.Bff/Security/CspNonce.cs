namespace Harmony.Web.Bff.Security;

using System.Security.Cryptography;

/// <summary>
/// The page's per-request nonce: 128 random bits, issued once per page request by the headers middleware and read by
/// the page renderer, so the CSP header and the page carry the same secret. A new one every request: a nonce read
/// from one page is worth nothing on the next.
/// </summary>
internal static class CspNonce
{
    private const string ItemKey = "Harmony.Web.Bff.CspNonce";

    /// <summary>What the web app's index.html carries where the nonce goes: &lt;app-root ngCspNonce="__CSP_NONCE__"&gt;.</summary>
    public const string Marker = "__CSP_NONCE__";

    public static string Issue(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[ItemKey] = nonce;
        return nonce;
    }

    public static string Of(HttpContext context)
        => context.Items[ItemKey] as string ?? throw new InvalidOperationException("The page's nonce was not issued: the security headers middleware runs before the page.");
}
