using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>
/// The hotel groups. Walled rows: inside a customer the filter does the work; the platform asks from outside by
/// naming the customer, with the tenant filter switched off, the way the hotels are read.
/// </summary>
public interface IPropertyGroupRepository : IRepository<PropertyGroup>
{
    /// <summary>Does this customer already have a group with the code (any case).</summary>
    Task<bool> CodeExistsInTenantAsync(Guid tenantId, string code, CancellationToken cancellationToken);

    /// <summary>The group, tracked, for a change by the platform; null when unknown.</summary>
    Task<PropertyGroup?> FindAcrossTenantsAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(PropertyGroup group, CancellationToken cancellationToken);

    /// <summary>The caller's own groups, untracked, inside the wall.</summary>
    IQueryable<PropertyGroup> Query();

    /// <summary>Every customer's groups, untracked, for the console's reads.</summary>
    IQueryable<PropertyGroup> QueryAcrossTenants();
}
