using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface ICurrencyRepository : IRepositoryLight<CurrencyRef>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<CurrencyRef?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task AddAsync(CurrencyRef currency, CancellationToken cancellationToken);

    IQueryable<CurrencyRef> Query();
}
