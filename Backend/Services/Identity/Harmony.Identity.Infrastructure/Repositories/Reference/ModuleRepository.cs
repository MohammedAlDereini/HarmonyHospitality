using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class ModuleRepository : ReferenceRepositoryBase<Module>, IModuleRepository
{
    public ModuleRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<Module?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Modules.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Modules.AnyAsync(c => c.Code == code, cancellationToken);
    }

    public async Task AddAsync(Module module, CancellationToken cancellationToken)
    {
        await this.Context.Modules.AddAsync(module, cancellationToken);
    }

    public IQueryable<Module> Query()
    {
        return this.Context.Modules.AsNoTracking();
    }
}
