using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>
/// The hotels. Walled rows: inside a customer the filter does the work; the platform asks from outside by naming the
/// customer, with the tenant filter switched off, the way the trail reads accounts.
/// </summary>
public interface IPropertyRepository : IRepository<Property>
{
    /// <summary>Does this customer already have a hotel with the code (any case).</summary>
    Task<bool> CodeExistsInTenantAsync(Guid tenantId, string code, CancellationToken cancellationToken);

    /// <summary>The hotel, tracked, for a change; null when unknown or not this customer's.</summary>
    Task<Property?> FindInTenantAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Property property, CancellationToken cancellationToken);

    /// <summary>The caller's own hotels, untracked, inside the wall: the tenant filter does the work.</summary>
    IQueryable<Property> Query();

    /// <summary>Every customer's hotels, untracked, for the console's reads.</summary>
    IQueryable<Property> QueryAcrossTenants();
}
