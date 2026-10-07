namespace Harmony.Identity.Domain.Services;

/// <summary>
/// The token engine's sessions. Ending all removes every sign-in session and every refresh token the person holds, so
/// nothing can be refreshed into a new token; it is called together with a SecurityVersion bump, which already kills the
/// access tokens. Ending one removes the sign-in session of one browser and the refresh tokens issued in it.
/// </summary>
public interface ISessionService
{
    Task EndAllAsync(Guid userId, CancellationToken cancellationToken);

    Task EndOneAsync(Guid userId, string sessionId, CancellationToken cancellationToken);

    /// <summary>The ids of the person's sign-in sessions that are still open.</summary>
    Task<IReadOnlySet<string>> OpenSessionIdsAsync(Guid userId, CancellationToken cancellationToken);
}
