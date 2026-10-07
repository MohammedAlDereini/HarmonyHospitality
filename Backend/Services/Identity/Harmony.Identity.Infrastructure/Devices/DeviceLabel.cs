namespace Harmony.Identity.Infrastructure.Devices;

/// <summary>
/// "Chrome on Windows" from a User-Agent header: our own few lines, not a parser library, because a label for a person is
/// all we need. The raw header is never stored. Order matters: Edge and Opera also say "Chrome", Chrome also says "Safari".
/// </summary>
public static class DeviceLabel
{
    public static string From(string? userAgent)
    {
        var ua = userAgent ?? string.Empty;
        return $"{Browser(ua)} on {System(ua)}";
    }

    private static string Browser(string ua)
        => Has(ua, "Edg/") ? "Edge"
            : Has(ua, "OPR/") ? "Opera"
            : Has(ua, "Firefox/") ? "Firefox"
            : Has(ua, "Chrome/") || Has(ua, "CriOS/") ? "Chrome"
            : Has(ua, "Safari/") ? "Safari"
            : "an unknown browser";

    private static string System(string ua)
        => Has(ua, "Windows") ? "Windows"
            : Has(ua, "iPhone") ? "iPhone"
            : Has(ua, "iPad") ? "iPad"
            : Has(ua, "Android") ? "Android"
            : Has(ua, "CrOS") ? "ChromeOS"
            : Has(ua, "Mac OS X") || Has(ua, "Macintosh") ? "Mac"
            : Has(ua, "Linux") ? "Linux"
            : "an unknown system";

    private static bool Has(string ua, string marker) => ua.Contains(marker, StringComparison.Ordinal);
}
