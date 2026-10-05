using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface ICityRepository : IRepositoryLight<City>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<City?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Another city of the country with this English name (the unique index), excluding one id when renaming.</summary>
    Task<bool> NameExistsAsync(string countryCode, string nameEn, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> CountryExistsAsync(string countryCode, CancellationToken cancellationToken);

    /// <summary>The subdivision exists and belongs to the country.</summary>
    Task<bool> SubdivisionBelongsAsync(string subdivisionCode, string countryCode, CancellationToken cancellationToken);

    Task<bool> TimeZoneExistsAsync(string timeZoneId, CancellationToken cancellationToken);

    Task AddAsync(City city, CancellationToken cancellationToken);

    IQueryable<City> Query();
}
