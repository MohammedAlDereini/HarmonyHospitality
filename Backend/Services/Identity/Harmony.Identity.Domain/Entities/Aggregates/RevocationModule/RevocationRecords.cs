using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.RevocationModule;

/// <summary>What kind of access ended: the whole account (suspended, terminated), a role, or the person's sessions.</summary>
public enum RevocationKind
{
    Account = 1,
    Role = 2,
    Session = 3,
}

/// <summary>
/// The feed's clock, one row per customer (design 4.1, the ERD's AuthorizationRevocationSequence): the high-water mark
/// only ever increases, and MinimumRetainedSequence says where the feed still begins after a compaction. The row is
/// locked for the length of the save that appends, so the feed has no holes and no out-of-order commits.
/// </summary>
public sealed class RevocationSequence : IMultiTenantEntity
{
    private RevocationSequence()
    {
    }

    public Guid TenantId { get; private set; }

    public long HighWatermark { get; private set; }

    public long MinimumRetainedSequence { get; private set; }

    public static RevocationSequence First(Guid tenantId) => new() { TenantId = tenantId, HighWatermark = 0, MinimumRetainedSequence = 1 };
}

/// <summary>"This person's access changed" (the ERD's AuthorizationRevocationFeed): append-only, ordered per customer.</summary>
public sealed class RevocationEntry : IMultiTenantEntity
{
    private RevocationEntry()
    {
    }

    public Guid TenantId { get; private set; }

    public long Sequence { get; private set; }

    public Guid UserAccountId { get; private set; }

    /// <summary>The SecurityOperation that caused it.</summary>
    public Guid CorrelationId { get; private set; }

    public RevocationKind Kind { get; private set; }

    /// <summary>The account's security version from now on: a token carrying an older one is dead (not in the ERD; what a reader compares).</summary>
    public int NewSecurityVersion { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static RevocationEntry Append(Guid tenantId, long sequence, Guid userAccountId, Guid operationId, RevocationKind kind, int newSecurityVersion, DateTimeOffset at)
        => new()
        {
            TenantId = tenantId,
            Sequence = sequence,
            UserAccountId = userAccountId,
            CorrelationId = operationId,
            Kind = kind,
            NewSecurityVersion = newSecurityVersion,
            OccurredAtUtc = at.ToUniversalTime(),
        };
}

/// <summary>
/// A compaction (the ERD's RevocationSnapshots): every account's security version at TakenAtSequence, so a service that
/// was offline for a day loads the snapshot and reads the feed after it, instead of replaying everything.
/// </summary>
public sealed class RevocationSnapshot : IMultiTenantEntity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private RevocationSnapshot()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public long TakenAtSequence { get; private set; }

    public DateTimeOffset TakenAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public static RevocationSnapshot Take(Guid tenantId, long atSequence, DateTimeOffset now)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, TakenAtSequence = atSequence, TakenAtUtc = now.ToUniversalTime(), ExpiresAtUtc = now.ToUniversalTime().Add(Lifetime) };
}

/// <summary>One account in a snapshot (the ERD's RevocationSnapshotItems), in pages ordered by PageKey.</summary>
public sealed class RevocationSnapshotItem : IMultiTenantEntity
{
    private RevocationSnapshotItem()
    {
    }

    public Guid SnapshotId { get; private set; }

    public long PageKey { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserAccountId { get; private set; }

    public int SecurityVersion { get; private set; }

    public static RevocationSnapshotItem Of(Guid snapshotId, long pageKey, Guid tenantId, Guid userAccountId, int securityVersion)
        => new() { SnapshotId = snapshotId, PageKey = pageKey, TenantId = tenantId, UserAccountId = userAccountId, SecurityVersion = securityVersion };
}

/// <summary>
/// "Everything up to here was pruned on purpose" (the ERD's RevocationGapTombstones). Without it a reader that skipped a
/// range cannot tell "nothing happened" from "I missed it", and would fail open. Every pruning leaves one.
/// </summary>
public sealed class RevocationGapTombstone : IMultiTenantEntity
{
    private RevocationGapTombstone()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public long ThroughSequence { get; private set; }

    public string Reason { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static RevocationGapTombstone Mark(Guid tenantId, long throughSequence, string reason, DateTimeOffset now)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, ThroughSequence = throughSequence, Reason = reason, CreatedAtUtc = now.ToUniversalTime() };
}
