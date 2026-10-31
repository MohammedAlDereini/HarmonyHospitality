using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Harmony.Web.Bff.ApiDocs;
using Harmony.Web.Bff.Settings;
using Microsoft.Extensions.Options;

namespace Harmony.Web.Bff.Health;

/// <summary>
/// GET /bff/health: Identity's detailed health report (every check by name), fetched inside the network with the ApiDocs
/// pair and served under our own origin, so the browser never talks to a service and never sees the password. Platform
/// people only: anyone else signed in gets 404, as /docs does; no session gets 401, as /bff/user does. Shaped for the
/// console: one status word, the checks as a list, and this host's own release line. Never cached.
/// </summary>
internal static class PlatformHealthDoor
{
    internal const string Path = "/bff/health";
    internal const string UnreachableStatus = "Unreachable";
    private const string HealthChecksPath = "/api/identity/healthchecks";
    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web);

    internal static async Task<IResult> GetAsync(
        HttpContext http,
        IHttpClientFactory clients,
        IOptions<ApiDocsSettings> docs,
        IOptions<BffSettings> bff,
        IOptions<ReleaseSettings> release,
        IWebHostEnvironment environment,
        ILogger<ApiDocsSettings> logger,
        CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (http.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        if (!await ApiDocsPage.IsPlatformPersonAsync(http))
        {
            return Results.NotFound();
        }

        var host = new HostLine(
            release.Value.Ring,
            release.Value.EffectiveVersion,
            release.Value.EffectiveMinimumClientVersion,
            environment.EnvironmentName.ToLowerInvariant());
        var checkedAtUtc = DateTimeOffset.UtcNow;
        var settings = docs.Value;

        using var request = new HttpRequestMessage(HttpMethod.Get, bff.Value.ApiDestination.TrimEnd('/') + HealthChecksPath);
        if (settings.HasCredentials)
        {
            var password = (await File.ReadAllTextAsync(settings.PasswordPath!, cancellationToken)).Trim();
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{password}")));
        }

        try
        {
            using var answer = await clients.CreateClient(ApiDocsPage.HttpClientName).SendAsync(request, cancellationToken);

            // The health page answers 200 when every check passed and 503 when one did not; both carry the report.
            if (answer.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable))
            {
                logger.LogWarning("Identity's health page answered {Status}.", (int)answer.StatusCode);
                return Unreachable(host, checkedAtUtc);
            }

            var report = await answer.Content.ReadFromJsonAsync<UiReport>(ReportJson, cancellationToken);
            if (report is null)
            {
                return Unreachable(host, checkedAtUtc);
            }

            var entries = (report.Entries ?? [])
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status ?? "Unhealthy",
                    duration = entry.Value.Duration,
                    description = entry.Value.Description,
                    tags = entry.Value.Tags ?? [],
                })
                .OrderBy(entry => entry.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Results.Json(new
            {
                status = report.Status ?? "Unhealthy",
                checkedAtUtc,
                totalDuration = report.TotalDuration,
                entries,
                host,
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Identity's health page could not be read.");
            return Unreachable(host, checkedAtUtc);
        }
    }

    /// <summary>Identity gave no report: 502 in the same shape, so the console can say "Identity is not answering".</summary>
    private static IResult Unreachable(HostLine host, DateTimeOffset checkedAtUtc)
        => Results.Json(
            new { status = UnreachableStatus, checkedAtUtc, totalDuration = (string?)null, entries = Array.Empty<object>(), host },
            statusCode: StatusCodes.Status502BadGateway);

    /// <summary>This BFF host as the console names it beside the services.</summary>
    private sealed record HostLine(string Ring, string Release, string MinimumClientVersion, string Environment);

    /// <summary>The HealthChecks.UI report as the framework writes it: status words, durations as text, entries by name.</summary>
    private sealed class UiReport
    {
        public string? Status { get; set; }

        public string? TotalDuration { get; set; }

        public Dictionary<string, UiEntry>? Entries { get; set; }
    }

    private sealed class UiEntry
    {
        public string? Status { get; set; }

        public string? Duration { get; set; }

        public string? Description { get; set; }

        public string[]? Tags { get; set; }
    }
}
