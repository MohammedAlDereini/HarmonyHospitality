using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class PropertyGroupRepository : BaseRepository<PropertyGroup>, IPropertyGroupRepository
{
    private readonly IdentityDbContext context;

    public PropertyGroupRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<PropertyGroup> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.PropertyGroups.FirstOrDefaultAsync(g => g.Id == (Guid)id, cancellationToken))!;
    }

    public async Task<bool> CodeExistsInTenantAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        // The tenant filter is dropped by name: the platform asks about a customer it is not inside of.
        return await this.context.PropertyGroups
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(g => g.TenantId == tenantId && g.Code == code, cancellationToken);
    }

    public async Task<PropertyGroup?> FindAcrossTenantsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.context.PropertyGroups
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Include(g => g.Brand)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task AddAsync(PropertyGroup group, CancellationToken cancellationToken)
    {
        await this.context.PropertyGroups.AddAsync(group, cancellationToken);
    }

    public IQueryable<PropertyGroup> Query()
    {
        return this.context.PropertyGroups.AsNoTracking();
    }

    public IQueryable<PropertyGroup> QueryAcrossTenants()
    {
        return this.context.PropertyGroups
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking();
    }
}
