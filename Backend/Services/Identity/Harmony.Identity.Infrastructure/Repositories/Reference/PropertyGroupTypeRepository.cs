using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class PropertyGroupTypeRepository : ReferenceRepositoryBase<PropertyGroupTypeRef>, IPropertyGroupTypeRepository
{
    public PropertyGroupTypeRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<PropertyGroupTypeRef?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.PropertyGroupTypes.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.PropertyGroupTypes.AnyAsync(t => t.Code == code, cancellationToken);
    }

    public async Task AddAsync(PropertyGroupTypeRef groupType, CancellationToken cancellationToken)
    {
        await this.Context.PropertyGroupTypes.AddAsync(groupType, cancellationToken);
    }

    public IQueryable<PropertyGroupTypeRef> Query()
    {
        return this.Context.PropertyGroupTypes.AsNoTracking();
    }
}
