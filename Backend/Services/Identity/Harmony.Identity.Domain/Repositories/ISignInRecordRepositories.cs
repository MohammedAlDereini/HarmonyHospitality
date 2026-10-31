using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SessionModule;
using Harmony.Identity.Domain.Entities.Aggregates.StepUpModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Sign-in records. Reads name the account or the session: a sign-in is written before the tenant wall applies.</summary>
public interface IOperatorSessionRepository : IRepository<OperatorSession>
{
    /// <summary>The open row of a Duende session, tracked; null when none is open.</summary>
    Task<OperatorSession?> OpenByKeyAsync(string sessionKey, CancellationToken cancellationToken);

    /// <summary>Every open row of an account, tracked.</summary>
    Task<IReadOnlyList<OperatorSession>> OpenOfAccountAsync(Guid userAccountId, CancellationToken cancellationToken);

    /// <summary>The account's newest sessions, untracked.</summary>
    Task<IReadOnlyList<OperatorSession>> LatestOfAccountAsync(Guid tenantId, Guid userAccountId, int take, CancellationToken cancellationToken);

    /// <summary>When each of these accounts last started a session; accounts that never did are absent.</summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> LatestStartsAsync(Guid tenantId, IReadOnlyCollection<Guid> userAccountIds, CancellationToken cancellationToken);

    /// <summary>A batch of open rows, oldest first, tracked: what the closing job compares with Duende's live sessions.</summary>
    Task<IReadOnlyList<OperatorSession>> OpenBatchAsync(int take, CancellationToken cancellationToken);

    /// <summary>Removes up to a batch of records that ended before the cutoff; the count removed.</summary>
    Task<int> DeleteEndedBeforeAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken);
}

public interface IStepUpAuthorizationRepository : IRepository<StepUpAuthorization>
{
    /// <summary>The account's newest proof that is still live, untracked; null when none is.</summary>
    Task<StepUpAuthorization?> LiveOfAccountAsync(Guid userAccountId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Removes up to a batch of proofs that expired before the cutoff; the count removed.</summary>
    Task<int> DeleteExpiredBeforeAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken);
}
