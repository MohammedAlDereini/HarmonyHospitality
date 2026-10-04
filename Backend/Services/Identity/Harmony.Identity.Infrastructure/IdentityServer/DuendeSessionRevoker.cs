using Duende.IdentityServer.Services;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// Duende's own revocation: every persisted grant of the subject (refresh tokens, for every client and device) is
/// removed from the operational store. Together with the SecurityVersion bump that always accompanies it, the person
/// is out everywhere and signs in again once.
/// </summary>
public sealed class DuendeSessionRevoker : ISessionRevoker
{
    private readonly IPersistedGrantService grants;
    private readonly ILogger<DuendeSessionRevoker> logger;

    public DuendeSessionRevoker(IPersistedGrantService grants, ILogger<DuendeSessionRevoker> logger)
    {
        this.grants = grants;
        this.logger = logger;
    }

    public async Task EndAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        await this.grants.RemoveAllGrantsAsync(userId.ToString("D"), cancellationToken);
        this.logger.LogInformation("All sessions ended for account {UserId}: refresh tokens removed.", userId);
    }
}
