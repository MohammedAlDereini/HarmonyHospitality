using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class TenantRepository : BaseRepository<Tenant>, ITenantRepository
{
    private readonly IdentityDbContext context;

    public TenantRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<Tenant> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.Tenants.FirstOrDefaultAsync(t => t.Id == (Guid)id, cancellationToken))!;
    }

    public async Task<Tenant?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.context.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.context.Tenants.AnyAsync(t => t.Code == code, cancellationToken);
    }

    public async Task<bool> CountryExistsAsync(string countryCode, CancellationToken cancellationToken)
    {
        return await this.context.Countries.AnyAsync(c => c.Code == countryCode, cancellationToken);
    }

    public async Task<bool> CurrencyExistsAsync(string currencyCode, CancellationToken cancellationToken)
    {
        return await this.context.Currencies.AnyAsync(c => c.Code == currencyCode, cancellationToken);
    }

    public async Task<TenantStatus?> StatusOfAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => (TenantStatus?)t.Status)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        await this.context.Tenants.AddAsync(tenant, cancellationToken);
    }

    public IQueryable<Tenant> Query()
    {
        return this.context.Tenants.AsNoTracking();
    }
}
