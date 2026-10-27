using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>
/// The customers' sign-in authorities and the e-mail domains their company logins cover. Walled rows; every read names
/// the customer (or the domain, at sign-in, where nobody is signed in yet), so the platform and the sign-in page use the
/// same doors as the customer.
/// </summary>
public interface IFederatedAuthorityRepository : IRepository<FederatedAuthority>
{
    /// <summary>A customer's authorities with their domains, tracked, local first.</summary>
    Task<IReadOnlyList<FederatedAuthority>> OfTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>One authority of a customer with its domains, tracked; null when it is not that customer's.</summary>
    Task<FederatedAuthority?> FindAsync(Guid tenantId, Guid authorityId, CancellationToken cancellationToken);

    Task<bool> HasLocalAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>True when any customer already has a company login with this issuer (one company signs in to one customer).</summary>
    Task<bool> IssuerInUseAsync(string issuer, CancellationToken cancellationToken);

    /// <summary>True when the domain is on any customer's list.</summary>
    Task<bool> DomainListedAsync(string domain, CancellationToken cancellationToken);

    /// <summary>The switched-on company login that lists the domain, with its domains, untracked; null when none does.</summary>
    Task<FederatedAuthority?> CoveringAsync(string domain, CancellationToken cancellationToken);

    /// <summary>The company login that lists the domain, on or off, with its domains, untracked; null when none does.</summary>
    Task<FederatedAuthority?> ListingAsync(string domain, CancellationToken cancellationToken);

    /// <summary>The company login of this kind with exactly this issuer, with its domains, untracked; null when none has it.</summary>
    Task<FederatedAuthority?> FindByIssuerAsync(AuthorityScheme scheme, string issuer, CancellationToken cancellationToken);
}
