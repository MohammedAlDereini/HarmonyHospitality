using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.MakerCheckerModule;
using Harmony.Identity.Domain.Entities.Aggregates.MergeApprovalModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Approval requests. Reads stay inside the caller's customer (the tenant wall applies).</summary>
public interface IMakerCheckerRequestRepository : IRepository<MakerCheckerRequest>
{
    /// <summary>The request with its version guard and execution, tracked; null when not the caller's customer's.</summary>
    Task<MakerCheckerRequest?> WithPartsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The customer's waiting requests, oldest first, untracked.</summary>
    Task<IReadOnlyList<MakerCheckerRequest>> PendingAsync(CancellationToken cancellationToken);
}

public interface IMergeApprovalRepository : IRepository<MergeApproval>
{
}
