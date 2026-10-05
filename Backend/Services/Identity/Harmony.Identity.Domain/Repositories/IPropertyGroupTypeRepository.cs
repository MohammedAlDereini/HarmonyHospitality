using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface IPropertyGroupTypeRepository : IRepositoryLight<PropertyGroupTypeRef>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<PropertyGroupTypeRef?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task AddAsync(PropertyGroupTypeRef groupType, CancellationToken cancellationToken);

    IQueryable<PropertyGroupTypeRef> Query();
}
