namespace Harmony.Web.Bff.Pages;

using System.Text.Json;
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
    private const string HeadEnd = "</head>";
    private static readonly JsonSerializerOptions ConfigJson = new(JsonSerializerDefaults.Web);

    public static async Task RenderAsync(HttpContext context, IWebHostEnvironment environment, IOptions<BffSettings> settings, IOptions<ReleaseSettings> release)
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
        var page = html.Replace(CspNonce.Marker, CspNonce.Of(context));
        await context.Response.WriteAsync(WithConfig(page, environment, release.Value), context.RequestAborted);
    }

    /// <summary>
    /// The runtime configuration element the app reads at boot (architecture D33): environment, release and the oldest
    /// client build allowed, as a JSON data block. A data block never executes, so the page's script policy does not
    /// apply to it; the serializer escapes every angle bracket, so no value can close the element early.
    /// </summary>
    private static string WithConfig(string html, IWebHostEnvironment environment, ReleaseSettings release)
    {
        var config = JsonSerializer.Serialize(
            new
            {
                environment = environment.EnvironmentName.ToLowerInvariant(),
                release = release.EffectiveVersion,
                minimumClientVersion = release.EffectiveMinimumClientVersion,
            },
            ConfigJson);
        var element = $"<script id=\"harmony-config\" type=\"application/json\">{config}</script>";
        var at = html.IndexOf(HeadEnd, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? html + element : html.Insert(at, element);
    }
}
