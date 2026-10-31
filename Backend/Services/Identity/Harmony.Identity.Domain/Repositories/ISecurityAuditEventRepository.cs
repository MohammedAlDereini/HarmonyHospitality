using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Reads only. Rows are written by the security journal and never changed; only the retention cleanup removes them.</summary>
public interface ISecurityAuditEventRepository : IRepository<SecurityAuditEvent>
{
    public IQueryable<SecurityAuditEvent> Query();

    /// <summary>Every tenant's rows, for the platform console's trail; the caller proves it is the platform first.</summary>
    public IQueryable<SecurityAuditEvent> QueryAcrossTenants();

    /// <summary>
    /// Deletes up to one batch of trail rows older than the cutoff, across every tenant (the retention law is the
    /// same for all), and returns how many went. The caller loops until a batch comes back empty.
    /// </summary>
    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken);
}
