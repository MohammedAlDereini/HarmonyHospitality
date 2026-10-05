using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class CountrySubdivisionRepository : ReferenceRepositoryBase<CountrySubdivision>, ICountrySubdivisionRepository
{
    public CountrySubdivisionRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<CountrySubdivision?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.CountrySubdivisions.FirstOrDefaultAsync(s => s.Code == code, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.CountrySubdivisions.AnyAsync(s => s.Code == code, cancellationToken);
    }

    public async Task<bool> CountryExistsAsync(string countryCode, CancellationToken cancellationToken)
    {
        return await this.Context.Countries.AnyAsync(c => c.Code == countryCode, cancellationToken);
    }

    public async Task AddAsync(CountrySubdivision subdivision, CancellationToken cancellationToken)
    {
        await this.Context.CountrySubdivisions.AddAsync(subdivision, cancellationToken);
    }

    public IQueryable<CountrySubdivision> Query()
    {
        return this.Context.CountrySubdivisions.AsNoTracking();
    }
}
