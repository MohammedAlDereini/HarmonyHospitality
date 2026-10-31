using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.EnrolmentModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

/// <summary>Reads drop the tenant filter by name and name the account or the customer: a first password is set anonymously.</summary>
public class LocalStaffEnrolmentRepository : BaseRepository<LocalStaffEnrolment>, ILocalStaffEnrolmentRepository
{
    private readonly IdentityDbContext context;

    public LocalStaffEnrolmentRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<LocalStaffEnrolment> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.Rows().FirstOrDefaultAsync(e => e.Id == (Guid)id, cancellationToken))!;
    }

    public Task<LocalStaffEnrolment?> LatestOfAccountAsync(Guid userAccountId, CancellationToken cancellationToken)
    {
        return this.Rows()
            .Where(e => e.UserAccountId == userAccountId)
            .OrderByDescending(e => e.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LocalStaffEnrolment>> PendingOfTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await this.Rows()
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.State == EnrolmentState.Pending)
            .OrderByDescending(e => e.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public IQueryable<LocalStaffEnrolment> QueryAcrossTenants()
    {
        return this.Rows().AsNoTracking();
    }

    private IQueryable<LocalStaffEnrolment> Rows()
    {
        return this.context.LocalStaffEnrolments.IgnoreQueryFilters([QueryFilterNames.Tenant]);
    }
}
