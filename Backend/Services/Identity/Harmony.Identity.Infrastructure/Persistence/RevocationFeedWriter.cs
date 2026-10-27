using Harmony.Identity.Domain.Entities.Aggregates.RevocationModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Persistence;

/// <summary>
/// The revocation feed's writer (stage C, design 4.1). Whatever save moves an account's SecurityVersion up also appends
/// one feed line for it, in the same transaction: the bump and the line commit together or not at all, so the feed can
/// never miss a revocation. Numbers come from the customer's sequence row, locked until the commit.
/// </summary>
internal static class RevocationFeedWriter
{
    /// <summary>True when this save bumps at least one account's SecurityVersion.</summary>
    public static bool HasBumps(IdentityDbContext context) => Bumps(context).Count > 0;

    /// <summary>Adds the operations and feed lines to the save; returns them, so a retried save can take them back out.</summary>
    public static async Task<List<object>> AppendAsync(IdentityDbContext context, CancellationToken cancellationToken)
    {
        var bumps = Bumps(context);
        var trail = context.ChangeTracker.Entries<SecurityAuditEvent>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        var now = DateTimeOffset.UtcNow;
        var added = new List<object>();

        // One lock per customer, always taken in the same order, so two saves never wait on each other in a circle.
        foreach (var tenant in bumps.GroupBy(u => u.TenantId).OrderBy(g => g.Key))
        {
            var last = await ReserveAsync(context, tenant.Key, tenant.Count(), cancellationToken);
            var sequence = last - tenant.Count();
            foreach (var user in tenant)
            {
                var action = trail.FirstOrDefault(t => t.UserAccountId == user.Id && KindOf(t.Action) is not null)?.Action
                    ?? trail.FirstOrDefault(t => t.UserAccountId == user.Id)?.Action;
                var operation = SecurityOperation.Named(tenant.Key, action ?? "account.sessions-ended", user.Id, now);
                var entry = RevocationEntry.Append(tenant.Key, ++sequence, user.Id, operation.Id, KindOf(action) ?? RevocationKind.Session, user.SecurityVersion, now);
                context.Add(operation);
                context.Add(entry);
                added.Add(operation);
                added.Add(entry);
            }
        }

        return added;
    }

    private static List<User> Bumps(IdentityDbContext context)
        => context.ChangeTracker.Entries<User>()
            .Where(e => e.State == EntityState.Modified && e.Entity.SecurityVersion > e.Property(u => u.SecurityVersion).OriginalValue)
            .Select(e => e.Entity)
            .ToList();

    // Suspended or terminated ends the whole account; a role change ends what the role allowed; everything else
    // (password, two-step, passkey, sign-out everywhere) ends the person's sessions.
    private static RevocationKind? KindOf(string? action) => action switch
    {
        SecurityAuditActions.AccountSuspended or SecurityAuditActions.AccountTerminated or SecurityAuditActions.AccountMerged => RevocationKind.Account,
        SecurityAuditActions.RolesChanged => RevocationKind.Role,
        null => null,
        _ => RevocationKind.Session,
    };

    // Creates the customer's row on its first revocation, then moves the high-water mark by the count. The UPDLOCK
    // holds the row until the transaction ends: a second writer waits, so numbers commit in order, without holes.
    private static async Task<long> ReserveAsync(IdentityDbContext context, Guid tenantId, int count, CancellationToken cancellationToken)
    {
        var rows = await context.Database.SqlQuery<long>($"""
            SET NOCOUNT ON;
            IF NOT EXISTS (SELECT 1 FROM [AuthorizationRevocationSequence] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId})
                INSERT INTO [AuthorizationRevocationSequence] ([TenantId], [HighWatermark], [MinimumRetainedSequence]) VALUES ({tenantId}, 0, 1);
            UPDATE [AuthorizationRevocationSequence] WITH (UPDLOCK, ROWLOCK)
               SET [HighWatermark] = [HighWatermark] + {count}
            OUTPUT inserted.[HighWatermark] AS [Value]
             WHERE [TenantId] = {tenantId};
            """).ToListAsync(cancellationToken);
        return rows.Single();
    }
}
