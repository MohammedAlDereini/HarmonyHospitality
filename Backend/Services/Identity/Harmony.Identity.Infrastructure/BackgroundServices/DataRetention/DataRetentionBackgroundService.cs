namespace Harmony.Identity.Infrastructure.BackgroundServices.DataRetention;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// The clock for the cleanup, the same shape as Duende's own token cleanup (a hosted background job): once shortly
/// after start, so a broken cleanup fails loudly on day one instead of at 03:00, then every night at the configured
/// UTC hour. A failed run only logs and the next night tries again: nothing a cleanup misses can hurt, it only waits.
/// </summary>
internal sealed class DataRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory scopes;
    private readonly DataRetentionSettings settings;
    private readonly ILogger<DataRetentionBackgroundService> logger;

    public DataRetentionBackgroundService(IServiceScopeFactory scopes, IOptions<DataRetentionSettings> options, ILogger<DataRetentionBackgroundService> logger)
    {
        this.scopes = scopes;
        this.settings = options.Value;
        this.logger = logger;
    }

    /// <summary>Time until the next occurrence of the cleanup hour, never zero or negative.</summary>
    internal static TimeSpan UntilNext(int hourUtc, DateTimeOffset nowUtc)
    {
        var next = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, hourUtc, 0, 0, TimeSpan.Zero);
        if (next <= nowUtc)
        {
            next = next.AddDays(1);
        }

        return next - nowUtc;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.settings.Enabled)
        {
            this.logger.LogInformation("Data retention cleanup is off: nothing old is ever removed on this instance.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(this.settings.StartDelaySeconds), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = this.scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IDataRetentionCleanupService>().CleanUpAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    this.logger.LogError(exception, "Data retention cleanup failed; the next night tries again.");
                }

                await Task.Delay(UntilNext(this.settings.CleanupHourUtc, DateTimeOffset.UtcNow), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown while waiting: nothing to finish, nothing lost.
        }
    }
}
