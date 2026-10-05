using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class CurrencyRepository : ReferenceRepositoryBase<CurrencyRef>, ICurrencyRepository
{
    public CurrencyRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<CurrencyRef?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Currencies.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Currencies.AnyAsync(c => c.Code == code, cancellationToken);
    }

    public async Task AddAsync(CurrencyRef currency, CancellationToken cancellationToken)
    {
        await this.Context.Currencies.AddAsync(currency, cancellationToken);
    }

    public IQueryable<CurrencyRef> Query()
    {
        return this.Context.Currencies.AsNoTracking();
    }
}
