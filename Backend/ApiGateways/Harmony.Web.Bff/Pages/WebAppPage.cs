namespace Harmony.Web.Bff.Pages;

using Harmony.Web.Bff.Security;
using Harmony.Web.Bff.Settings;
using Microsoft.Extensions.Options;

/// <summary>
/// The web app's one page per language, rendered per request: wwwroot/{language}/index.html with the nonce marker
/// replaced by this request's nonce. Never served without asking (no-cache): every copy carries its own nonce, and a
/// deployment shows at once; not no-store, which would also forbid the browser's back/forward cache for a page that
/// holds nothing personal. The hashed assets next to it stay plain static files, cached as their names allow.
/// </summary>
internal static class WebAppPage
{
    public static async Task RenderAsync(HttpContext context, IWebHostEnvironment environment, IOptions<BffSettings> settings)
    {
        // A door that answers nothing for this path (a GET on a POST-only door, a path nobody mapped) is a plain 404:
        // the page is for the app's own addresses only, the way Duende's catch-all excludes the BFF and API paths.
        if (DoorPaths.IsDoor(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var language = PageLanguage.Of(context, settings.Value);
        var file = environment.WebRootFileProvider.GetFileInfo($"{language}/index.html");
        if (!file.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        string html;
        using (var reader = new StreamReader(file.CreateReadStream()))
        {
            html = await reader.ReadToEndAsync(context.RequestAborted);
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.ContentLanguage = language;
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.WriteAsync(html.Replace(CspNonce.Marker, CspNonce.Of(context)), context.RequestAborted);
    }
}
