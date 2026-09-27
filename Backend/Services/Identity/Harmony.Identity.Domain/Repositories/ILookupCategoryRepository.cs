using System.Linq.Expressions;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Aggregates.LookupModule;

namespace Harmony.Identity.Domain.Repositories;

public interface ILookupCategoryRepository : IRepository<LookupCategory>
{
    public new Task<bool> AnyAsync(Expression<Func<LookupCategory, bool>> predicate, CancellationToken cancellationToken);

    public IQueryable<LookupCategory> Query();
}