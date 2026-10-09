using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class UserDeviceRepository : BaseRepository<UserDevice>, IUserDeviceRepository
{
    private readonly IdentityDbContext context;

    public UserDeviceRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public async Task<UserDevice?> FindByKeyAsync(Guid userId, string keyHash, CancellationToken cancellationToken)
    {
        return await this.context.UserDevices
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(d => d.UserAccountId == userId && d.KeyHash == keyHash, cancellationToken);
    }

    public async Task<UserDevice?> FindAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken)
    {
        return await this.context.UserDevices
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(d => d.UserAccountId == userId && d.Id == deviceId, cancellationToken);
    }

    public IQueryable<UserDevice> Query()
    {
        return this.context.UserDevices.AsNoTracking();
    }

    public void Forget(UserDevice device)
    {
        this.context.Entry(device).State = EntityState.Detached;
    }

    public async Task<int> DeleteUnseenSinceAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        // The cleanup serves every tenant, so the tenant filter is lifted - or the job would happily delete nothing.
        return await this.context.UserDevices
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(d => d.LastSeenUtc < cutoffUtc)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
