namespace Harmony.Web.Bff.Security;

using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

/// <summary>
/// Where the browser sends CSP and Trusted Types violations (report-to for today's browsers, report-uri for older
/// ones; both named in the page policy). Anonymous: a report carries no session and no custom header, so it sits
/// outside the CSRF wall. What keeps it harmless: it does nothing but write one log line, takes only the two report
/// content types (no HTML form can send either), reads at most 8 KB, and is rate-limited per address.
/// </summary>
internal static class CspViolationReports
{
    public const string Path = "/bff/csp-report";
    public const string RateLimitPolicy = "csp-report";
    private const int MaxBodyBytes = 8 * 1024;
    private const int SampleLength = 40;

    public static async Task ReceiveAsync(HttpContext context, ILoggerFactory loggerFactory)
    {
        var contentType = context.Request.ContentType ?? string.Empty;
        var legacy = contentType.StartsWith("application/csp-report", StringComparison.OrdinalIgnoreCase);
        var reportingApi = contentType.StartsWith("application/reports+json", StringComparison.OrdinalIgnoreCase);
        if (!legacy && !reportingApi)
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

        var logger = loggerFactory.CreateLogger("Harmony.Web.Bff.Csp");
        using (document)
        {
            var root = document.RootElement;
            if (legacy)
            {
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("csp-report", out var report))
                {
                    Log(logger, report, legacy: true);
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var type) && type.ValueEquals("csp-violation") && item.TryGetProperty("body", out var body))
                    {
                        Log(logger, body, legacy: false);
                    }
                }
            }
        }

        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static void Log(ILogger logger, JsonElement report, bool legacy)
    {
        string Text(string legacyName, string name)
            => report.TryGetProperty(legacy ? legacyName : name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;

        var sample = Text("script-sample", "sample");
        logger.LogWarning(
            "CSP {Disposition}: {Directive} blocked {Blocked} on {Page} ({Source}:{Line}) sample \"{Sample}\"",
            Text("disposition", "disposition"),
            Text("effective-directive", "effectiveDirective"),
            PathOnly(Text("blocked-uri", "blockedURL")),
            PathOnly(Text("document-uri", "documentURL")),
            PathOnly(Text("source-file", "sourceFile")),
            report.TryGetProperty(legacy ? "line-number" : "lineNumber", out var line) && line.ValueKind == JsonValueKind.Number ? line.ToString() : "-",
            sample.Length <= SampleLength ? sample : sample[..SampleLength]);
    }

    // Paths only: a query string could carry something personal; "inline", "eval" and "" stay as they are.
    private static string PathOnly(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.AbsolutePath : value;
}
