using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class PermissionRepository : BaseRepository<Permission>, IPermissionRepository
{
    private readonly IdentityDbContext context;

    public PermissionRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<Permission> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.Permissions
            .FirstOrDefaultAsync(p => p.Id == (Guid)id, cancellationToken))!;
    }

    public IQueryable<Permission> Query()
    {
        return this.context.Permissions.AsNoTracking();
    }
}
