using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SessionModule;
using Harmony.Identity.Domain.Entities.Aggregates.StepUpModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class OperatorSessionRepository : BaseRepository<OperatorSession>, IOperatorSessionRepository
{
    private readonly IdentityDbContext context;

    public OperatorSessionRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<OperatorSession> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.Rows().FirstOrDefaultAsync(s => s.Id == (Guid)id, cancellationToken))!;
    }

    public Task<OperatorSession?> OpenByKeyAsync(string sessionKey, CancellationToken cancellationToken)
    {
        return this.Rows().FirstOrDefaultAsync(s => s.SessionKey == sessionKey && s.EndedAtUtc == null, cancellationToken);
    }

    public async Task<IReadOnlyList<OperatorSession>> OpenOfAccountAsync(Guid userAccountId, CancellationToken cancellationToken)
    {
        return await this.Rows().Where(s => s.UserAccountId == userAccountId && s.EndedAtUtc == null).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperatorSession>> LatestOfAccountAsync(Guid tenantId, Guid userAccountId, int take, CancellationToken cancellationToken)
    {
        return await this.Rows()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.UserAccountId == userAccountId)
            .OrderByDescending(s => s.StartedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperatorSession>> OpenBatchAsync(int take, CancellationToken cancellationToken)
    {
        return await this.Rows().Where(s => s.EndedAtUtc == null).OrderBy(s => s.StartedAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> DeleteEndedBeforeAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken)
        => this.Rows().Where(s => s.EndedAtUtc != null && s.EndedAtUtc < cutoffUtc).Take(batchSize).ExecuteDeleteAsync(cancellationToken);

    private IQueryable<OperatorSession> Rows() => this.context.OperatorSessions.IgnoreQueryFilters([QueryFilterNames.Tenant]);
}

public class StepUpAuthorizationRepository : BaseRepository<StepUpAuthorization>, IStepUpAuthorizationRepository
{
    private readonly IdentityDbContext context;

    public StepUpAuthorizationRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<StepUpAuthorization> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.StepUpAuthorizations.IgnoreQueryFilters([QueryFilterNames.Tenant]).FirstOrDefaultAsync(s => s.Id == (Guid)id, cancellationToken))!;
    }

    public Task<StepUpAuthorization?> LiveOfAccountAsync(Guid userAccountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return this.context.StepUpAuthorizations
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .Where(s => s.OperatorUserAccountId == userAccountId && s.ExpiresAtUtc > now)
            .OrderByDescending(s => s.ExpiresAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> DeleteExpiredBeforeAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken)
        => this.context.StepUpAuthorizations.IgnoreQueryFilters([QueryFilterNames.Tenant]).Where(s => s.ExpiresAtUtc < cutoffUtc).Take(batchSize).ExecuteDeleteAsync(cancellationToken);
}
