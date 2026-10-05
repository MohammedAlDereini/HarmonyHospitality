using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class LanguageRepository : ReferenceRepositoryBase<Language>, ILanguageRepository
{
    public LanguageRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<Language?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Languages.FirstOrDefaultAsync(l => l.Code == code, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Languages.AnyAsync(l => l.Code == code, cancellationToken);
    }

    public async Task AddAsync(Language language, CancellationToken cancellationToken)
    {
        await this.Context.Languages.AddAsync(language, cancellationToken);
    }

    public IQueryable<Language> Query()
    {
        return this.Context.Languages.AsNoTracking();
    }
}
