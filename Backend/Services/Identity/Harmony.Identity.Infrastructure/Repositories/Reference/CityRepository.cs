using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class CityRepository : ReferenceRepositoryBase<City>, ICityRepository
{
    public CityRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<City?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.Context.Cities.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string countryCode, string nameEn, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await this.Context.Cities.AnyAsync(
            c => c.CountryCode == countryCode && c.NameEn == nameEn && (excludeId == null || c.Id != excludeId),
            cancellationToken);
    }

    public async Task<bool> CountryExistsAsync(string countryCode, CancellationToken cancellationToken)
    {
        return await this.Context.Countries.AnyAsync(c => c.Code == countryCode, cancellationToken);
    }

    public async Task<bool> SubdivisionBelongsAsync(string subdivisionCode, string countryCode, CancellationToken cancellationToken)
    {
        return await this.Context.CountrySubdivisions.AnyAsync(s => s.Code == subdivisionCode && s.CountryCode == countryCode, cancellationToken);
    }

    public async Task<bool> TimeZoneExistsAsync(string timeZoneId, CancellationToken cancellationToken)
    {
        return await this.Context.TimeZones.AnyAsync(z => z.Id == timeZoneId, cancellationToken);
    }

    public async Task AddAsync(City city, CancellationToken cancellationToken)
    {
        await this.Context.Cities.AddAsync(city, cancellationToken);
    }

    public IQueryable<City> Query()
    {
        return this.Context.Cities.AsNoTracking();
    }
}
