namespace Harmony.Identity.Infrastructure.BackgroundServices.SessionRecords;

using Harmony.Identity.Domain.Entities.Aggregates.SessionModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sessions that end without anybody saying so (60 idle minutes, the absolute limit, a browser simply closed) leave Duende's
/// table on their own. Every few minutes this job compares the open records with Duende's live sessions and ends the
/// records whose session is gone ("expired"). A failed round only logs; the next round tries again.
/// </summary>
internal sealed class SessionRecordClosingService : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(5);
    private const int BatchSize = 500;

    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<SessionRecordClosingService> logger;

    public SessionRecordClosingService(IServiceScopeFactory scopes, ILogger<SessionRecordClosingService> logger)
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
                try
                {
                    using var scope = this.scopes.CreateScope();
                    var closed = await CloseGoneAsync(scope.ServiceProvider, stoppingToken);
                    if (closed > 0)
                    {
                        this.logger.LogInformation("{Count} session record(s) ended: their sign-in sessions had expired.", closed);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    this.logger.LogError(exception, "Closing expired session records failed; the next round tries again.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown while waiting: nothing to finish.
        }
    }

    internal static async Task<int> CloseGoneAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var records = services.GetRequiredService<IOperatorSessionRepository>();
        var sessions = services.GetRequiredService<ISessionService>();
        var open = await records.OpenBatchAsync(BatchSize, cancellationToken);
        var closed = 0;
        foreach (var person in open.GroupBy(r => r.UserAccountId))
        {
            var alive = await sessions.OpenSessionIdsAsync(person.Key, cancellationToken);
            foreach (var record in person.Where(r => !alive.Contains(r.SessionKey)))
            {
                record.End(DateTimeOffset.UtcNow, OperatorSession.Expired);
                closed++;
            }
        }

        if (closed > 0)
        {
            await records.SaveChangesAsync(cancellationToken);
        }

        return closed;
    }
}
