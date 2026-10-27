using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Identity.Domain.Entities.Aggregates.RevocationModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class RevocationFeedRepository : IRevocationFeedRepository
{
    private const string Pruned = "compacted: lines older than the retention window";

    private readonly IdentityDbContext context;

    public RevocationFeedRepository(IdentityDbContext context)
    {
        this.context = context;
    }

    public Task<RevocationSequence?> SequenceOfAsync(Guid tenantId, CancellationToken cancellationToken)
        => this.context.RevocationSequences.IgnoreQueryFilters([QueryFilterNames.Tenant]).AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<RevocationEntry>> FeedAfterAsync(Guid tenantId, long afterSequence, int take, CancellationToken cancellationToken)
        => await this.context.RevocationFeed.IgnoreQueryFilters([QueryFilterNames.Tenant]).AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Sequence > afterSequence)
            .OrderBy(e => e.Sequence)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<RevocationGapTombstone?> LatestTombstoneAsync(Guid tenantId, CancellationToken cancellationToken)
        => this.context.RevocationGapTombstones.IgnoreQueryFilters([QueryFilterNames.Tenant]).AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.ThroughSequence)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<RevocationSnapshot?> LatestSnapshotAsync(Guid tenantId, CancellationToken cancellationToken)
        => this.context.RevocationSnapshots.IgnoreQueryFilters([QueryFilterNames.Tenant]).AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.TakenAtSequence)
            .ThenByDescending(s => s.TakenAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RevocationSnapshotItem>> SnapshotPageAsync(Guid snapshotId, long afterPageKey, int take, CancellationToken cancellationToken)
        => await this.context.RevocationSnapshotItems.IgnoreQueryFilters([QueryFilterNames.Tenant]).AsNoTracking()
            .Where(i => i.SnapshotId == snapshotId && i.PageKey > afterPageKey)
            .OrderBy(i => i.PageKey)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> TenantsWithFeedAsync(CancellationToken cancellationToken)
        => await this.context.RevocationSequences.IgnoreQueryFilters([QueryFilterNames.Tenant]).AsNoTracking().Select(s => s.TenantId).ToListAsync(cancellationToken);

    public Task<RevocationCompaction> CompactAsync(Guid tenantId, DateTimeOffset cutoffUtc, DateTimeOffset now, CancellationToken cancellationToken)
        => this.context.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await this.context.Database.BeginTransactionAsync(ct);

            // The same lock the writer takes: while it is held no revocation of this customer commits, so the
            // snapshot and the high-water mark agree.
            var marks = await this.context.Database.SqlQuery<long>($"""
                SELECT [HighWatermark] AS [Value] FROM [AuthorizationRevocationSequence] WITH (UPDLOCK, ROWLOCK) WHERE [TenantId] = {tenantId}
                """).ToListAsync(ct);
            if (marks.Count == 0)
            {
                return new RevocationCompaction(null, 0, 0, 0);
            }

            var high = marks[0];
            var cutoff = cutoffUtc.ToUniversalTime();
            var through = await this.context.RevocationFeed.IgnoreQueryFilters([QueryFilterNames.Tenant])
                .Where(e => e.TenantId == tenantId && e.OccurredAtUtc < cutoff)
                .MaxAsync(e => (long?)e.Sequence, ct);
            if (through is null)
            {
                await transaction.CommitAsync(ct);
                return new RevocationCompaction(null, 0, 0, 0);
            }

            // One snapshot per high-water mark: a second compaction with nothing new reuses it.
            var snapshot = await this.context.RevocationSnapshots.IgnoreQueryFilters([QueryFilterNames.Tenant]).AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.TakenAtSequence == high, ct);
            if (snapshot is null)
            {
                snapshot = RevocationSnapshot.Take(tenantId, high, now);
                this.context.RevocationSnapshots.Add(snapshot);
                await this.context.SaveChangesAsync(ct);
                await this.context.Database.ExecuteSqlAsync($"""
                    INSERT INTO [RevocationSnapshotItems] ([SnapshotId], [PageKey], [TenantId], [UserAccountId], [SecurityVersion])
                    SELECT {snapshot.Id}, ROW_NUMBER() OVER (ORDER BY [Id]), [TenantId], [Id], [SecurityVersion]
                      FROM [UserAccounts] WHERE [TenantId] = {tenantId}
                    """, ct);
            }

            var removed = await this.context.RevocationFeed.IgnoreQueryFilters([QueryFilterNames.Tenant])
                .Where(e => e.TenantId == tenantId && e.Sequence <= through)
                .ExecuteDeleteAsync(ct);
            await this.context.Database.ExecuteSqlAsync($"""
                UPDATE [AuthorizationRevocationSequence]
                   SET [MinimumRetainedSequence] = CASE WHEN [MinimumRetainedSequence] > {through.Value + 1} THEN [MinimumRetainedSequence] ELSE {through.Value + 1} END
                 WHERE [TenantId] = {tenantId}
                """, ct);
            this.context.RevocationGapTombstones.Add(RevocationGapTombstone.Mark(tenantId, through.Value, Pruned, now));

            // Old snapshots go once they expire; the newest always stays, it is where a reader starts.
            await this.context.RevocationSnapshots.IgnoreQueryFilters([QueryFilterNames.Tenant])
                .Where(s => s.TenantId == tenantId && s.ExpiresAtUtc < now && s.Id != snapshot.Id)
                .ExecuteDeleteAsync(ct);
            await this.context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new RevocationCompaction(snapshot.Id, snapshot.TakenAtSequence, through.Value, removed);
        }, cancellationToken);
}
