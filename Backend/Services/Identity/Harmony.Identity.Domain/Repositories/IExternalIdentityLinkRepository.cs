using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.IdentityLinkModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Company identities bound to accounts, with their periods. Every read names the company login or the customer.</summary>
public interface IExternalIdentityLinkRepository : IRepository<ExternalIdentityLink>
{
    /// <summary>The link of a company identity, with its periods, tracked; null when the identity was never linked.</summary>
    Task<ExternalIdentityLink?> FindAsync(Guid authorityId, string providerSubject, CancellationToken cancellationToken);

    /// <summary>An account's links with their periods, tracked, newest first.</summary>
    Task<IReadOnlyList<ExternalIdentityLink>> OfAccountAsync(Guid tenantId, Guid userAccountId, CancellationToken cancellationToken);
}
