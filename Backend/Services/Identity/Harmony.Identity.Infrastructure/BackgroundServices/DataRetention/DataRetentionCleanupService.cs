namespace Harmony.Identity.Infrastructure.BackgroundServices.DataRetention;

using Harmony.Identity.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// One cleanup run: the trail keeps its legal twelve months and a device unseen past its days is forgotten, so the
/// next sign-in from it warns again. Deletes run in batches and every batch commits alone: a restart mid-run loses
/// nothing, the next run simply continues where the rows end.
/// </summary>
public interface IDataRetentionCleanupService
{
    public Task CleanUpAsync(CancellationToken cancellationToken);
}

internal sealed class DataRetentionCleanupService : IDataRetentionCleanupService
{
    private readonly ISecurityAuditEventRepository trail;
    private readonly IUserDeviceRepository devices;
    private readonly DataRetentionSettings settings;
    private readonly ILogger<DataRetentionCleanupService> logger;

    public DataRetentionCleanupService(ISecurityAuditEventRepository trail, IUserDeviceRepository devices, IOptions<DataRetentionSettings> options, ILogger<DataRetentionCleanupService> logger)
    {
        this.trail = trail;
        this.devices = devices;
        this.settings = options.Value;
        this.logger = logger;
    }

    public async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var trailGone = await DrainAsync(cutoff => this.trail.DeleteOlderThanAsync(cutoff, this.settings.DeleteBatchSize, cancellationToken), now.AddMonths(-this.settings.SecurityTrailMonths));
        var devicesGone = await DrainAsync(cutoff => this.devices.DeleteUnseenSinceAsync(cutoff, this.settings.DeleteBatchSize, cancellationToken), now.AddDays(-this.settings.UnseenDeviceDays));

        this.logger.LogInformation(
            "Data retention cleanup done: {Trail} trail row(s) past {TrailMonths} months and {Devices} device(s) unseen {DeviceDays} days removed.",
            trailGone,
            this.settings.SecurityTrailMonths,
            devicesGone,
            this.settings.UnseenDeviceDays);
    }

    private static async Task<int> DrainAsync(Func<DateTimeOffset, Task<int>> deleteBatch, DateTimeOffset cutoff)
    {
        var total = 0;
        int batch;
        do
        {
            batch = await deleteBatch(cutoff);
            total += batch;
        }
        while (batch > 0);

        return total;
    }
}
