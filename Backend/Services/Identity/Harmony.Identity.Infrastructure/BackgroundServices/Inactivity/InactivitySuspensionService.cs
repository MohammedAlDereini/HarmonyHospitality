namespace Harmony.Identity.Infrastructure.BackgroundServices.Inactivity;

using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Stage E, the inactivity rule (PCI DSS 8.2.6, CIS 5.3): every active account of an active customer that has not signed
/// in for the customer's InactivityDays (90 unless it chose fewer) is suspended with the reason, its sessions end, and the
/// trail says so. History stays; an administrator turns the person back on. The company directory's people are its own
/// business, and the platform's break-glass administrator (the super role) is never touched. At most 200 accounts per
/// customer a round; the rest wait for the next one.
/// </summary>
public sealed class InactivitySuspension : IInactivitySuspension
{
    private const int BatchSize = 200;

    private readonly ITenantRepository tenants;
    private readonly IUserAccountRepository accounts;
    private readonly ISecurityAuditEventRepository trail;
    private readonly ISecurityEventFactory events;
    private readonly ICacheService cache;
    private readonly ISessionService sessions;
    private readonly TimeProvider clock;

    public InactivitySuspension(ITenantRepository tenants, IUserAccountRepository accounts, ISecurityAuditEventRepository trail, ISecurityEventFactory events, ICacheService cache, ISessionService sessions, TimeProvider clock)
    {
        this.tenants = tenants;
        this.accounts = accounts;
        this.trail = trail;
        this.events = events;
        this.cache = cache;
        this.sessions = sessions;
        this.clock = clock;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = this.clock.GetUtcNow();
        var customers = await this.tenants.Query()
            .Where(t => t.Status == TenantStatus.Active)
            .Select(t => new { t.Id, t.InactivityDays })
            .ToListAsync(cancellationToken);

        var suspended = 0;
        foreach (var customer in customers)
        {
            var dormant = await this.accounts.DormantAsync(customer.Id, now.AddDays(-customer.InactivityDays), BatchSize, cancellationToken);
            if (dormant.Count == 0)
            {
                continue;
            }

            foreach (var account in dormant)
            {
                account.Suspend($"No sign-in for {customer.InactivityDays} days.");
                await this.trail.AddAsync(this.events.Create(account, SecurityAuditActions.AccountSuspended, $"inactive {customer.InactivityDays} days"), cancellationToken);
            }

            // One save per customer: the suspensions, their trail rows and their revocation lines together.
            await this.accounts.SaveChangesAsync(cancellationToken);
            foreach (var account in dormant)
            {
                await this.cache.AddAsync(SecurityVersionCacheKeys.UserVersionKey(account.TenantId, account.Id), account.SecurityVersion, SecurityVersionCacheKeys.UserVersionLifetime, cancellationToken);
                await this.sessions.EndAllAsync(account.Id, cancellationToken);
            }

            suspended += dormant.Count;
        }

        return suspended;
    }
}

/// <summary>Runs the inactivity rule once a day, the first time ten minutes after the start. A failed round only logs.</summary>
internal sealed class InactivitySuspensionService : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromDays(1);
    private static readonly TimeSpan FirstRoundAfter = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<InactivitySuspensionService> logger;

    public InactivitySuspensionService(IServiceScopeFactory scopes, ILogger<InactivitySuspensionService> logger)
    {
        this.scopes = scopes;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRoundAfter, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = this.scopes.CreateScope();
                    var suspended = await scope.ServiceProvider.GetRequiredService<IInactivitySuspension>().RunAsync(stoppingToken);
                    if (suspended > 0)
                    {
                        this.logger.LogInformation("{Count} account(s) suspended after too many days without a sign-in.", suspended);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    this.logger.LogError(exception, "Suspending inactive accounts failed; the next round tries again.");
                }

                await Task.Delay(Every, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown while waiting: nothing to finish.
        }
    }
}
