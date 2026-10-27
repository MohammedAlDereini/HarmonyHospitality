using Harmony.Identity.Domain.Entities.Aggregates.RevocationModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>What one compaction did: the snapshot it took (or found), and how far the feed was pruned.</summary>
public sealed record RevocationCompaction(Guid? SnapshotId, long SnapshotAt, long PrunedThrough, int Removed);

/// <summary>
/// The revocation feed (stage C). Lines are written by the save itself (never through here); this reads them for the
/// platform and compacts old ones into a snapshot. Every read names the customer: the feed sits outside the tenant wall.
/// </summary>
public interface IRevocationFeedRepository
{
    /// <summary>The customer's clock, untracked; null before its first revocation.</summary>
    Task<RevocationSequence?> SequenceOfAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Lines after the given number, oldest first, untracked.</summary>
    Task<IReadOnlyList<RevocationEntry>> FeedAfterAsync(Guid tenantId, long afterSequence, int take, CancellationToken cancellationToken);

    /// <summary>The customer's newest tombstone, untracked; null when nothing was ever pruned.</summary>
    Task<RevocationGapTombstone?> LatestTombstoneAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The customer's newest snapshot, untracked; null when none was taken.</summary>
    Task<RevocationSnapshot?> LatestSnapshotAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>One page of a snapshot, by PageKey, untracked.</summary>
    Task<IReadOnlyList<RevocationSnapshotItem>> SnapshotPageAsync(Guid snapshotId, long afterPageKey, int take, CancellationToken cancellationToken);

    /// <summary>Every customer that has a feed.</summary>
    Task<IReadOnlyList<Guid>> TenantsWithFeedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Snapshots every account's SecurityVersion at the current high-water mark, then removes lines older than the cutoff
    /// and leaves a tombstone. Nothing older than the cutoff: nothing happens (a null snapshot, 0 removed).
    /// </summary>
    Task<RevocationCompaction> CompactAsync(Guid tenantId, DateTimeOffset cutoffUtc, DateTimeOffset now, CancellationToken cancellationToken);
}
