namespace Harmony.Web.Bff.Pages;

using Harmony.Web.Bff.Settings;

/// <summary>
/// Which locale build answers this request. One folder per language under wwwroot (D7), chosen in this order: a
/// language named on the path (/ar, /ar/, /ar/index.html), the preference cookie, the browser's Accept-Language, the
/// default. ?lang=ar on a page sets the cookie and comes back to the same address: URLs stay identical in both
/// languages, and the cookie carries the choice until the server-side profile does (D26).
/// </summary>
internal static class PageLanguage
{
    private const string ItemKey = "Harmony.Web.Bff.PageLanguage";

    /// <summary>The preference cookie: one word, en or ar. __Host- means Secure, Path=/, no Domain; HttpOnly because the page tells the app its language through lang=, not the cookie.</summary>
    public const string CookieName = "__Host-harmony-lang";

    public const string Query = "lang";

    public static string Of(HttpContext context, BffSettings settings)
    {
        if (context.Items[ItemKey] is string named)
        {
            return named;
        }

        if (context.Request.Cookies.TryGetValue(CookieName, out var preferred) && settings.Languages.Contains(preferred, StringComparer.Ordinal))
        {
            return preferred;
        }

        foreach (var accepted in context.Request.GetTypedHeaders().AcceptLanguage.OrderByDescending(a => a.Quality ?? 1))
        {
            var tag = accepted.Value.Value;
            if (tag is { Length: >= 2 })
            {
                var language = tag[..2].ToLowerInvariant();
                if (settings.Languages.Contains(language, StringComparer.Ordinal))
                {
                    return language;
                }
            }
        }

        return settings.DefaultLanguage;
    }

    public static void Remember(HttpContext context, string language)
        => context.Response.Cookies.Append(CookieName, language, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(365),
            IsEssential = true,
        });

    public static void Set(HttpContext context, string language) => context.Items[ItemKey] = language;

    /// <summary>"/ar", "/ar/" or "/ar/index.html" for a language the settings know: the page, in that language.</summary>
    public static bool IsLocaleIndex(PathString path, BffSettings settings, out string language)
    {
        var segments = path.Value?.Trim('/').Split('/') ?? [];
        if (segments.Length is 1 or 2 && segments[0].Length == 2 && settings.Languages.Contains(segments[0], StringComparer.Ordinal)
            && (segments.Length == 1 || segments[1].Equals("index.html", StringComparison.OrdinalIgnoreCase)))
        {
            language = segments[0];
            return true;
        }

        language = string.Empty;
        return false;
    }
}
