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
}
