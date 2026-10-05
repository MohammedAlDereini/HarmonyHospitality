using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class CountryRepository : ReferenceRepositoryBase<Country>, ICountryRepository
{
    public CountryRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<Country?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Countries.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Countries.AnyAsync(c => c.Code == code, cancellationToken);
    }

    public async Task<bool> Alpha3ExistsAsync(string alpha3, CancellationToken cancellationToken)
    {
        return await this.Context.Countries.AnyAsync(c => c.Alpha3 == alpha3, cancellationToken);
    }

    public async Task<bool> CurrencyExistsAsync(string currencyCode, CancellationToken cancellationToken)
    {
        return await this.Context.Currencies.AnyAsync(c => c.Code == currencyCode, cancellationToken);
    }

    public async Task AddAsync(Country country, CancellationToken cancellationToken)
    {
        await this.Context.Countries.AddAsync(country, cancellationToken);
    }

    public IQueryable<Country> Query()
    {
        return this.Context.Countries.AsNoTracking();
    }
}
