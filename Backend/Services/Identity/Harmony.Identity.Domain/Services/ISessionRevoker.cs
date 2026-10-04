namespace Harmony.Identity.Domain.Services;

/// <summary>
/// Ends every session a person holds: the refresh tokens stored by the token engine are removed, so nothing can be
/// refreshed into a new token. Called together with a SecurityVersion bump, which already kills the access tokens.
/// </summary>
public interface ISessionRevoker
{
    Task EndAllAsync(Guid userId, CancellationToken cancellationToken);
}
