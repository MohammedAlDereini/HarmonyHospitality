using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>The customers' rows. Platform data: no tenant filter applies, the doors are the wall.</summary>
public interface ITenantRepository : IRepository<Tenant>
{
    /// <summary>The row, tracked, for a change; null when unknown.</summary>
    Task<Tenant?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    Task<bool> CountryExistsAsync(string countryCode, CancellationToken cancellationToken);

    Task<bool> CurrencyExistsAsync(string currencyCode, CancellationToken cancellationToken);

    /// <summary>The status alone, untracked, for the sign-in wall; null when the tenant has no row.</summary>
    Task<TenantStatus?> StatusOfAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken);

    IQueryable<Tenant> Query();
}
