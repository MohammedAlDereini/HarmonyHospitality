namespace Harmony.Identity.Domain.Services;

/// <summary>A company directory that proved itself: its connection and the customer it pushes to.</summary>
public sealed record DirectoryCaller(Guid ConnectionId, Guid TenantId, string ClientId);

/// <summary>A short-lived token for a directory (OAuth 2.0 client credentials).</summary>
public sealed record DirectoryAccessToken(string Value, int ExpiresInSeconds);

/// <summary>
/// Stage E's door: how a company directory proves itself. Two ways, one secret. Microsoft Entra ID trades the client id
/// and secret for a one-hour token at our token door (OAuth 2.0 client credentials, IPSIE's way). Okta's header login,
/// Entra's "Secret Token" and Mews' own SCIM page send one long-lived token instead: "clientId.secret". Either dies the
/// moment the secret is replaced or the connection is switched off; a suspended customer is refused too.
/// </summary>
public interface IDirectoryCredentials
{
    /// <summary>The caller behind a bearer value, or null.</summary>
    Task<DirectoryCaller?> AuthenticateAsync(string bearer, CancellationToken cancellationToken);

    /// <summary>A one-hour token for the client id and secret, or null.</summary>
    Task<DirectoryAccessToken?> IssueAsync(string clientId, string secret, CancellationToken cancellationToken);
}
