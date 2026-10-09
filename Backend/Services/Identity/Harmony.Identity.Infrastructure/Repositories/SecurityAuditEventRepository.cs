using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class SecurityAuditEventRepository : BaseRepository<SecurityAuditEvent>, ISecurityAuditEventRepository
{
    private readonly IdentityDbContext context;

    public SecurityAuditEventRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public IQueryable<SecurityAuditEvent> Query()
    {
        return this.context.SecurityAuditEvents.AsNoTracking();
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        // The cleanup serves every tenant, so the tenant filter is lifted - or the job would happily delete nothing.
        return await this.context.SecurityAuditEvents
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(e => e.OccurredAtUtc < cutoffUtc)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
