using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface ILanguageRepository : IRepositoryLight<Language>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<Language?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task AddAsync(Language language, CancellationToken cancellationToken);

    IQueryable<Language> Query();
}
