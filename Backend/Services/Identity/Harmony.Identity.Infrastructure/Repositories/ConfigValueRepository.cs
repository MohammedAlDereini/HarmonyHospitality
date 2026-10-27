using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class ConfigValueRepository : BaseRepository<ConfigValue>, IConfigValueRepository
{
    private readonly IdentityDbContext context;

    public ConfigValueRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<ConfigValue> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.ConfigValues.FirstOrDefaultAsync(v => v.Id == (Guid)id, cancellationToken))!;
    }

    public Task<ConfigValue?> FindAsync(Guid tenantId, ConfigScope place, string key, CancellationToken cancellationToken)
    {
        return this.context.ConfigValues
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Scope == place.Level && v.ScopeId == place.Id && v.Key == key, cancellationToken);
    }

    public async Task AddAsync(ConfigValue value, CancellationToken cancellationToken)
    {
        await this.context.ConfigValues.AddAsync(value, cancellationToken);
    }

    public void Remove(ConfigValue value)
    {
        this.context.ConfigValues.Remove(value);
    }

    public async Task<IReadOnlyList<ConfigValue>> ForHotelAsync(Guid tenantId, Guid propertyId, IReadOnlyCollection<Guid> groupIds, IReadOnlyCollection<string>? keys, CancellationToken cancellationToken)
    {
        // The tenant filter is dropped by name and the customer named instead: the platform resolves a customer's hotel too.
        var values = this.context.ConfigValues
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId)
            .Where(v => v.Scope == ConfigScopeLevel.Tenant
                || (v.Scope == ConfigScopeLevel.Property && v.ScopeId == propertyId)
                || (v.Scope == ConfigScopeLevel.PropertyGroup && groupIds.Contains(v.ScopeId)));

        if (keys is not null)
        {
            values = values.Where(v => keys.Contains(v.Key));
        }

        return await values.ToListAsync(cancellationToken);
    }

    public IQueryable<ConfigValue> Query()
    {
        return this.context.ConfigValues.AsNoTracking();
    }
}
