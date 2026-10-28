using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.MakerCheckerModule;
using Harmony.Identity.Domain.Entities.Aggregates.MergeApprovalModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class MakerCheckerRequestRepository : BaseRepository<MakerCheckerRequest>, IMakerCheckerRequestRepository
{
    private readonly IdentityDbContext context;

    public MakerCheckerRequestRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<MakerCheckerRequest> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => (await this.WithPartsAsync((Guid)id, cancellationToken))!;

    public Task<MakerCheckerRequest?> WithPartsAsync(Guid id, CancellationToken cancellationToken)
        => this.context.Set<MakerCheckerRequest>()
            .Include(r => r.ExpectedVersions)
            .Include(r => r.Executions).ThenInclude(e => e.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MakerCheckerRequest>> PendingAsync(CancellationToken cancellationToken)
        => await this.context.Set<MakerCheckerRequest>()
            .AsNoTracking()
            .Where(r => r.State == RequestState.Pending)
            .OrderBy(r => r.RaisedAtUtc)
            .ToListAsync(cancellationToken);
}

public class MergeApprovalRepository : BaseRepository<MergeApproval>, IMergeApprovalRepository
{
    public MergeApprovalRepository(IdentityDbContext context)
        : base(context)
    {
    }
}
