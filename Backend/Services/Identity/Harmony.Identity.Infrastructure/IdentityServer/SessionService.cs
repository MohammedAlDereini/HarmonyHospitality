using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// The sign-in sessions and refresh tokens, kept and ended by Duende; Harmony stores none of them. End all: every sign-in
/// session of the person (server-side sessions, so the sign-in cookies stop working at once) and every persisted grant
/// (the refresh tokens the BFF holds for them); with the SecurityVersion bump that always comes with it, the person is
/// out everywhere. End one: the session of one browser and its refresh tokens.
/// List open: which sessions are still alive, for the device list, read from Duende's session store by the exact subject
/// (an index lookup); the management service's QuerySessionsAsync is a text search that reads the whole table.
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly ISessionManagementService sessions;
    private readonly IPersistedGrantService grants;
    private readonly IServerSideSessionStore sessionStore;
    private readonly ILogger<SessionService> logger;

    public SessionService(ISessionManagementService sessions, IPersistedGrantService grants, IServerSideSessionStore sessionStore, ILogger<SessionService> logger)
    {
        this.sessions = sessions;
        this.grants = grants;
        this.sessionStore = sessionStore;
        this.logger = logger;
    }

    public async Task EndAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var subject = userId.ToString("D");
        await this.sessions.RemoveSessionsAsync(new RemoveSessionsContext { SubjectId = subject, RevokeConsents = false }, cancellationToken);
        await this.grants.RemoveAllGrantsAsync(subject, cancellationToken);
        this.logger.LogInformation("All sessions ended for account {UserId}: sign-in sessions and refresh tokens removed.", userId);
    }

    public async Task EndOneAsync(Guid userId, string sessionId, CancellationToken cancellationToken)
    {
        // That browser's sign-in cookie stops working, and the refresh tokens issued in this session are revoked.
        await this.sessions.RemoveSessionsAsync(
            new RemoveSessionsContext { SubjectId = userId.ToString("D"), SessionId = sessionId, RevokeConsents = false },
            cancellationToken);
        this.logger.LogInformation("One sign-in session ended for account {UserId}.", userId);
    }

    public async Task<IReadOnlySet<string>> OpenSessionIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await this.sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = userId.ToString("D") }, cancellationToken);
        var now = DateTime.UtcNow;
        return rows
            .Where(s => s.SessionId is not null && (s.Expires is null || s.Expires > now))
            .Select(s => s.SessionId)
            .ToHashSet(StringComparer.Ordinal);
    }
}
