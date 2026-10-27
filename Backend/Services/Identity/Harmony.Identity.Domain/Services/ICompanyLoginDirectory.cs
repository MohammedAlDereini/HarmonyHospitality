using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;

namespace Harmony.Identity.Domain.Services;

/// <summary>
/// What Identity knows about Harmony's own apps at Microsoft and Google: whether each is set up, the audience their tokens
/// carry, and how to find a company there. Infrastructure answers from IdentitySettings:CompanyLogin.
/// </summary>
public interface ICompanyLoginDirectory
{
    /// <summary>True when Harmony has an app at the provider (its settings section is present).</summary>
    bool IsConfigured(AuthorityScheme scheme);

    /// <summary>Harmony's app id at the provider: the audience a company's tokens carry.</summary>
    string AudienceOf(AuthorityScheme scheme);

    /// <summary>
    /// Who signs the company's tokens. Microsoft: read from Microsoft's public metadata for the company's domain (the
    /// company's own tenant id is inside). Google: one issuer for every company. Null when the provider knows no company
    /// under the domain.
    /// </summary>
    Task<string?> IssuerOfAsync(AuthorityScheme scheme, string domain, CancellationToken cancellationToken);

    /// <summary>The link the company's IT opens once so its people may sign in to Harmony (Microsoft's admin consent); null for Google.</summary>
    string? ConsentUrlOf(FederatedAuthority authority);
}
