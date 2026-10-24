namespace Harmony.Web.Bff.Telemetry;

using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

/// <summary>
/// Where the web app posts what it measured and what went wrong (architecture D9, ASVS 16): page timings, the web
/// vitals, and error references. Inside the session and the CSRF wall like every /bff door. What keeps it harmless:
/// it does nothing but write log lines, takes JSON only, reads at most 8 KB and 20 events, keeps only the fields it
/// names (every value cut to a short length), never a free text, never anything personal, and is rate-limited per
/// address. The log lines are structured: one event per line, UTC, the keys named here.
/// </summary>
internal static class TelemetryDoor
{
    public const string Path = "/bff/telemetry";
    public const string RateLimitPolicy = "telemetry";
    private const int MaxBodyBytes = 8 * 1024;
    private const int MaxEvents = 20;
    private const int MaxTextLength = 80;

    public static async Task ReceiveAsync(HttpContext context, ILoggerFactory loggerFactory)
    {
        var contentType = context.Request.ContentType ?? string.Empty;
        if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        var size = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (size is { IsReadOnly: false })
        {
            size.MaxRequestBodySize = MaxBodyBytes;
        }

        if (context.Request.ContentLength > MaxBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        catch (BadHttpRequestException)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        var logger = loggerFactory.CreateLogger("Harmony.Web.Bff.Telemetry");
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var count = 0;
            foreach (var item in events.EnumerateArray())
            {
                if (++count > MaxEvents)
                {
                    break;
                }

                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = Text(item, "name");
                if (name.Length == 0)
                {
                    continue;
                }

                logger.LogInformation(
                    "Web {Event} at {At} value {Value} route {Route} code {Code} release {Release} browser {Browser}",
                    name,
                    Text(item, "at"),
                    item.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0d,
                    Text(item, "route"),
                    Text(item, "code"),
                    Text(item, "release"),
                    Text(item, "browser"));
            }
        }

        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    // Only the named keys, only strings, only a short length: a query string, a name or a free text never gets through.
    private static string Text(JsonElement item, string key)
    {
        if (!item.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        var text = value.GetString() ?? string.Empty;
        return text.Length <= MaxTextLength ? text : text[..MaxTextLength];
    }
}
