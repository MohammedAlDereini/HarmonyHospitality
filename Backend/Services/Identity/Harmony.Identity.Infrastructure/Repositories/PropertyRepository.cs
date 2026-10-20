using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class PropertyRepository : BaseRepository<Property>, IPropertyRepository
{
    private readonly IdentityDbContext context;

    public PropertyRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<Property> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.Properties.FirstOrDefaultAsync(p => p.Id == (Guid)id, cancellationToken))!;
    }

    public async Task<bool> CodeExistsInTenantAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        // The tenant filter is dropped by name: the platform asks about a customer it is not inside of.
        return await this.context.Properties
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(p => p.TenantId == tenantId && p.Code == code, cancellationToken);
    }

    public async Task<Property?> FindInTenantAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        return await this.context.Properties
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, cancellationToken);
    }

    public async Task AddAsync(Property property, CancellationToken cancellationToken)
    {
        await this.context.Properties.AddAsync(property, cancellationToken);
    }

    public IQueryable<Property> Query()
    {
        return this.context.Properties.AsNoTracking();
    }

    public async Task<Property?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.context.Properties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public IQueryable<Property> QueryAcrossTenants()
    {
        return this.context.Properties
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking();
    }
}
