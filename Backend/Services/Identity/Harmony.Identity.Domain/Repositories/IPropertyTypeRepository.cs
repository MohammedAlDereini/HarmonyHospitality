using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface IPropertyTypeRepository : IRepositoryLight<PropertyTypeRef>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<PropertyTypeRef?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task AddAsync(PropertyTypeRef propertyType, CancellationToken cancellationToken);

    IQueryable<PropertyTypeRef> Query();
}
