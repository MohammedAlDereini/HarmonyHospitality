using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class PropertyTypeRepository : ReferenceRepositoryBase<PropertyTypeRef>, IPropertyTypeRepository
{
    public PropertyTypeRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<PropertyTypeRef?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.PropertyTypes.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        return await this.Context.PropertyTypes.AnyAsync(t => t.Code == code, cancellationToken);
    }

    public async Task AddAsync(PropertyTypeRef propertyType, CancellationToken cancellationToken)
    {
        await this.Context.PropertyTypes.AddAsync(propertyType, cancellationToken);
    }

    public IQueryable<PropertyTypeRef> Query()
    {
        return this.Context.PropertyTypes.AsNoTracking();
    }
}
