using System.Net.Http.Headers;
using System.Text;
using Harmony.Web.Bff.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Harmony.Web.Bff.ApiDocs;

/// <summary>
/// The one API-docs page of the platform: /docs shows every listed service in Swagger UI's "Select a definition"
/// dropdown; /docs/{service}.json is that service's document, fetched here inside the network (with the ApiDocs
/// credentials when the services ask for them) and served under our own origin, so the browser never talks to a service
/// and never sees a password. Platform people only: anyone else, signed in or not, gets 404 — nothing is advertised.
/// </summary>
internal static class ApiDocsPage
{
    internal const string HttpClientName = "api-docs";
    private const string TenantClaim = "tenant_id";
    internal const string PlatformTenantKey = "ApplicationSettings:ApplicationKeys:PlatformTenantId";
    private static readonly JsonWebTokenHandler TokenReader = new();

    /// <summary>The wall in front of everything under /docs: a session whose access token names the platform tenant.</summary>
    internal static async Task<bool> IsPlatformPersonAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var platformTenant = context.RequestServices.GetRequiredService<IConfiguration>()[PlatformTenantKey];
        var accessToken = await context.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(platformTenant) || string.IsNullOrEmpty(accessToken) || !TokenReader.CanReadToken(accessToken))
        {
            return false;
        }

        // Read, not validated: the token came to us from the token endpoint over TLS and is stored sealed in Redis.
        var tenant = new JsonWebToken(accessToken).TryGetClaim(TenantClaim, out var claim) ? claim.Value : null;
        return string.Equals(tenant, platformTenant, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>GET /docs/{service}.json: the named service's document, or 404 for a name not on the list.</summary>
    internal static async Task<IResult> GetDocumentAsync(string service, IHttpClientFactory clients, IOptions<ApiDocsSettings> options, ILogger<ApiDocsSettings> logger, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var entry = settings.Services.FirstOrDefault(candidate => string.Equals(candidate.Name, service, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return Results.NotFound();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, entry.Url);
        if (settings.HasCredentials)
        {
            var password = (await File.ReadAllTextAsync(settings.PasswordPath!, cancellationToken)).Trim();
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{password}")));
        }

        try
        {
            using var answer = await clients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            if (!answer.IsSuccessStatusCode)
            {
                logger.LogWarning("The API document of {Service} answered {Status}.", entry.Name, (int)answer.StatusCode);
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }

            var document = await answer.Content.ReadAsStringAsync(cancellationToken);
            return Results.Text(document, "application/json", Encoding.UTF8);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "The API document of {Service} could not be fetched.", entry.Name);
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
