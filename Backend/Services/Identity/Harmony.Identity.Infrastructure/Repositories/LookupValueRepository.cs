using System.Linq.Expressions;
using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Aggregates.LookupModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class LookupValueRepository : BaseRepository<LookupValue>, ILookupValueRepository
{
    private readonly IdentityDbContext context;
    private readonly ICallContext callContext;

    public LookupValueRepository(IdentityDbContext context, ICallContext callContext)
        : base(context)
    {
        this.context = context;
        this.callContext = callContext;
    }

    public new async Task<bool> AnyAsync(Expression<Func<LookupValue, bool>> predicate, CancellationToken cancellationToken)
    {
        return await this.Visible().AnyAsync(predicate, cancellationToken);
    }

    public override async Task<LookupValue> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        var tenantId = this.CurrentTenantId();

        return (await this.context.LookupValues
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == (Guid)id && !v.IsDeleted && v.TenantId == tenantId, cancellationToken))!;
    }

    public IQueryable<LookupValue> QueryForCategory(Guid lookupCategoryId)
    {
        return this.Visible().Where(v => v.LookupCategoryId == lookupCategoryId);
    }

    private IQueryable<LookupValue> Visible()
    {
        var tenantId = this.CurrentTenantId();

        return this.context.LookupValues
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => !v.IsDeleted && (v.TenantId == tenantId || v.IsGlobal));
    }

    private Guid CurrentTenantId()
    {
        return Guid.TryParse(this.callContext.TenantId(), out var tenantId) ? tenantId : Guid.Empty;
    }
}