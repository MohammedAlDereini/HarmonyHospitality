using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Reads only. Rows are written by the security journal and never changed.</summary>
public interface ISecurityAuditEventRepository : IRepository<SecurityAuditEvent>
{
    public IQueryable<SecurityAuditEvent> Query();
}
