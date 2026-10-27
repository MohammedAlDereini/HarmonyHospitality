namespace Harmony.Identity.Infrastructure.BackgroundServices.RevocationFeed;

using Harmony.Identity.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Every hour, each customer's feed lines older than 7 days go into a snapshot, and a tombstone marks the cut, so the
/// feed stays short and a reader that was away for long starts from the snapshot. A failed customer only logs.
/// </summary>
internal sealed class RevocationCompactionService : BackgroundService
{
    public static readonly TimeSpan Keep = TimeSpan.FromDays(7);
    private static readonly TimeSpan Every = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<RevocationCompactionService> logger;

    public RevocationCompactionService(IServiceScopeFactory scopes, ILogger<RevocationCompactionService> logger)
    {
        this.scopes = scopes;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(Every, stoppingToken);
                List<Guid> tenants;
                try
                {
                    using var scope = this.scopes.CreateScope();
                    tenants = [.. await scope.ServiceProvider.GetRequiredService<IRevocationFeedRepository>().TenantsWithFeedAsync(stoppingToken)];
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    this.logger.LogError(exception, "Listing the revocation feeds failed; the next round tries again.");
                    continue;
                }

                foreach (var tenantId in tenants)
                {
                    try
                    {
                        using var scope = this.scopes.CreateScope();
                        var now = DateTimeOffset.UtcNow;
                        var result = await scope.ServiceProvider.GetRequiredService<IRevocationFeedRepository>().CompactAsync(tenantId, now - Keep, now, stoppingToken);
                        if (result.Removed > 0)
                        {
                            this.logger.LogInformation("Revocation feed of tenant {TenantId} compacted through {Through}: {Removed} line(s) into snapshot {SnapshotId}.", tenantId, result.PrunedThrough, result.Removed, result.SnapshotId);
                        }
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        this.logger.LogError(exception, "Compacting the revocation feed of tenant {TenantId} failed; the next round tries again.", tenantId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown while waiting: nothing to finish.
        }
    }
}
