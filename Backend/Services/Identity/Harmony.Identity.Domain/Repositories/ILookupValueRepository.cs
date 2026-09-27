using System.Linq.Expressions;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;

namespace Harmony.Identity.Domain.Repositories;

public interface ILookupValueRepository : IRepository<LookupValue>
{
    public new Task<bool> AnyAsync(Expression<Func<LookupValue, bool>> predicate, CancellationToken cancellationToken);

    public IQueryable<LookupValue> QueryForCategory(Guid lookupCategoryId);
}