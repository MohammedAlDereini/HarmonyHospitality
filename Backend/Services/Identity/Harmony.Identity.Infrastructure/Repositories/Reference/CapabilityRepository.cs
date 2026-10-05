using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class CapabilityRepository : ReferenceRepositoryBase<Capability>, ICapabilityRepository
{
    public CapabilityRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<Capability?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Capabilities.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.Capabilities.AnyAsync(c => c.Code == code, cancellationToken);
    }

    public async Task AddAsync(Capability capability, CancellationToken cancellationToken)
    {
        await this.Context.Capabilities.AddAsync(capability, cancellationToken);
    }

    public IQueryable<Capability> Query()
    {
        return this.Context.Capabilities.AsNoTracking();
    }
}
