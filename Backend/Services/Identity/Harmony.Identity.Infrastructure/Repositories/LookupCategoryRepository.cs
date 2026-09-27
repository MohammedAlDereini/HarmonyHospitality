using System.Linq.Expressions;
using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class LookupCategoryRepository : BaseRepository<LookupCategory>, ILookupCategoryRepository
{
    private readonly IdentityDbContext context;
    private readonly ICallContext callContext;

    public LookupCategoryRepository(IdentityDbContext context, ICallContext callContext)
        : base(context)
    {
        this.context = context;
        this.callContext = callContext;
    }

    public new async Task<bool> AnyAsync(Expression<Func<LookupCategory, bool>> predicate, CancellationToken cancellationToken)
    {
        return await this.Visible().AnyAsync(predicate, cancellationToken);
    }

    public override async Task<LookupCategory> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        var tenantId = this.CurrentTenantId();

        return (await this.context.LookupCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == (Guid)id && !c.IsDeleted && c.TenantId == tenantId, cancellationToken))!;
    }

    public IQueryable<LookupCategory> Query()
    {
        return this.Visible();
    }

    private IQueryable<LookupCategory> Visible()
    {
        var tenantId = this.CurrentTenantId();

        return this.context.LookupCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => !c.IsDeleted && (c.TenantId == tenantId || c.IsGlobal));
    }

    private Guid CurrentTenantId()
    {
        return Guid.TryParse(this.callContext.TenantId(), out var tenantId) ? tenantId : Guid.Empty;
    }
}