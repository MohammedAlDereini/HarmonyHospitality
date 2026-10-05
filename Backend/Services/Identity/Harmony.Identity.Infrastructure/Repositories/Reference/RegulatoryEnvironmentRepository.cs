using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class RegulatoryEnvironmentRepository : ReferenceRepositoryBase<RegulatoryEnvironment>, IRegulatoryEnvironmentRepository
{
    public RegulatoryEnvironmentRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<RegulatoryEnvironment?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.RegulatoryEnvironments
            .FirstOrDefaultAsync(e => e.Code == code, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.RegulatoryEnvironments
            .AnyAsync(e => e.Code == code, cancellationToken);
    }

    public async Task<bool> CountryExistsAsync(string countryCode, CancellationToken cancellationToken)
    {
        return await this.Context.Countries
            .AnyAsync(c => c.Code == countryCode, cancellationToken);
    }

    public async Task AddAsync(RegulatoryEnvironment environment, CancellationToken cancellationToken)
    {
        await this.Context.RegulatoryEnvironments.AddAsync(environment, cancellationToken);
    }

    public IQueryable<RegulatoryEnvironment> Query()
    {
        return this.Context.RegulatoryEnvironments.AsNoTracking();
    }
}
