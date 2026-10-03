using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class RoleRepository : BaseRepository<Role>, IRoleRepository
{
    private readonly IdentityDbContext context;

    public RoleRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<Role> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.Roles
            .FirstOrDefaultAsync(r => r.Id == (Guid)id, cancellationToken))!;
    }

    public async Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToUpperInvariant();

        return await this.context.Roles
            .FirstOrDefaultAsync(r => r.Code == normalized, cancellationToken);
    }

    public async Task<Role?> GetWithPermissionsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public IQueryable<Role> Query()
    {
        return this.context.Roles.AsNoTracking();
    }
}