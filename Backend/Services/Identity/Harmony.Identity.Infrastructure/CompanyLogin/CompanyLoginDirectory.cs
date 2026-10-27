using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Infrastructure.CompanyLogin;

/// <summary>Answers from IdentitySettings:CompanyLogin, and finds a Microsoft company by reading Microsoft's public metadata.</summary>
internal sealed class CompanyLoginDirectory : ICompanyLoginDirectory
{
    public const string ClientName = "company-login";

    private readonly CompanyLoginSettings settings;
    private readonly IHttpClientFactory clients;
    private readonly ILogger<CompanyLoginDirectory> logger;

    public CompanyLoginDirectory(IOptions<CompanyLoginSettings> settings, IHttpClientFactory clients, ILogger<CompanyLoginDirectory> logger)
    {
        this.settings = settings.Value;
        this.clients = clients;
        this.logger = logger;
    }

    public bool IsConfigured(AuthorityScheme scheme) => scheme switch
    {
        AuthorityScheme.Microsoft => this.settings.Microsoft.IsPresent,
        AuthorityScheme.Google => this.settings.Google.IsPresent,
        _ => false,
    };

    public string AudienceOf(AuthorityScheme scheme) => scheme switch
    {
        AuthorityScheme.Microsoft => this.settings.Microsoft.ClientId,
        AuthorityScheme.Google => this.settings.Google.ClientId,
        _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "Harmony's own sign-in has no audience."),
    };

    public async Task<string?> IssuerOfAsync(AuthorityScheme scheme, string domain, CancellationToken cancellationToken)
    {
        if (scheme == AuthorityScheme.Google)
        {
            return this.settings.Google.Authority;
        }

        if (scheme != AuthorityScheme.Microsoft)
        {
            return null;
        }

        // Microsoft answers for any of a company's domains with that company's own issuer; an unknown domain is a 400.
        var microsoft = this.settings.Microsoft;
        try
        {
            using var response = await this.clients.CreateClient(ClientName).GetAsync(microsoft.MetadataOf(domain), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                this.logger.LogInformation("Microsoft knows no company under {Domain} ({Status}).", domain, (int)response.StatusCode);
                return null;
            }

            var metadata = await response.Content.ReadFromJsonAsync<Metadata>(cancellationToken);
            var issuer = metadata?.Issuer;
            return issuer is not null && microsoft.TenantOf(issuer) is not null ? issuer : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            this.logger.LogWarning(exception, "Microsoft's metadata for {Domain} could not be read.", domain);
            return null;
        }
    }

    public string? ConsentUrlOf(FederatedAuthority authority)
    {
        if (authority.Scheme != AuthorityScheme.Microsoft || !this.settings.Microsoft.IsPresent)
        {
            return null;
        }

        var microsoft = this.settings.Microsoft;
        return microsoft.TenantOf(authority.Issuer) is { } tenant
            ? $"{microsoft.Instance}{tenant}/adminconsent?client_id={Uri.EscapeDataString(microsoft.ClientId)}"
            : null;
    }

    private sealed record Metadata([property: JsonPropertyName("issuer")] string? Issuer);
}
