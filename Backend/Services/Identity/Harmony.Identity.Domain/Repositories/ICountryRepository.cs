using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface ICountryRepository : IRepositoryLight<Country>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<Country?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task<bool> Alpha3ExistsAsync(string alpha3, CancellationToken cancellationToken);

    Task<bool> CurrencyExistsAsync(string currencyCode, CancellationToken cancellationToken);

    Task AddAsync(Country country, CancellationToken cancellationToken);

    IQueryable<Country> Query();
}
