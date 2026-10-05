using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface ICountrySubdivisionRepository : IRepositoryLight<CountrySubdivision>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<CountrySubdivision?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task<bool> CountryExistsAsync(string countryCode, CancellationToken cancellationToken);

    Task AddAsync(CountrySubdivision subdivision, CancellationToken cancellationToken);

    IQueryable<CountrySubdivision> Query();
}
