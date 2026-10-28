using System.Globalization;
using Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Provisioning;

/// <summary>
/// Both forms carry the client id first, "clientId.rest", so the rate limit can count per directory before anything is
/// checked. The rest is the 43-character secret itself, or a one-hour token: our Data Protection key ring (the one Duende's
/// stored tokens use) seals the connection and the moment its secret was made, so a new secret ends every token at once.
/// </summary>
public sealed class DirectoryCredentials : IDirectoryCredentials
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    private const string Purpose = "Harmony.Identity.Directory.AccessToken.v1";
    private const int SecretLength = 43;

    private readonly IProvisioningConnectionRepository connections;
    private readonly ITenantRepository tenants;
    private readonly ITimeLimitedDataProtector protector;
    private readonly TimeProvider clock;
    private readonly ILogger<DirectoryCredentials> logger;

    public DirectoryCredentials(IProvisioningConnectionRepository connections, ITenantRepository tenants, IDataProtectionProvider protection, TimeProvider clock, ILogger<DirectoryCredentials> logger)
    {
        this.connections = connections;
        this.tenants = tenants;
        this.protector = protection.CreateProtector(Purpose).ToTimeLimitedDataProtector();
        this.clock = clock;
        this.logger = logger;
    }

    /// <summary>The client id a bearer value names, for counting; null when it names none.</summary>
    public static string? ClientIdOf(string? bearer)
    {
        var dot = bearer?.IndexOf('.') ?? -1;
        return dot == ProvisioningConnection.ClientIdLength ? bearer![..dot] : null;
    }

    public async Task<DirectoryCaller?> AuthenticateAsync(string bearer, CancellationToken cancellationToken)
    {
        var clientId = ClientIdOf(bearer);
        if (clientId is null)
        {
            return null;
        }

        var rest = bearer[(clientId.Length + 1)..];
        var connection = await this.UsableAsync(clientId, cancellationToken);
        if (connection is null)
        {
            return null;
        }

        var proved = rest.Length == SecretLength ? connection.Accepts(rest) : this.Opens(rest, connection);
        if (!proved)
        {
            this.logger.LogWarning("A directory call for client {ClientId} was refused: wrong secret or token.", clientId);
            return null;
        }

        return new DirectoryCaller(connection.Id, connection.TenantId, connection.ClientId);
    }

    public async Task<DirectoryAccessToken?> IssueAsync(string clientId, string secret, CancellationToken cancellationToken)
    {
        if (clientId is not { Length: ProvisioningConnection.ClientIdLength })
        {
            return null;
        }

        var connection = await this.UsableAsync(clientId, cancellationToken);
        if (connection is null || !connection.Accepts(secret))
        {
            this.logger.LogWarning("A directory token was refused for client {ClientId}.", clientId);
            return null;
        }

        var sealedPart = this.protector.Protect(Seal(connection), this.clock.GetUtcNow().Add(TokenLifetime));
        return new DirectoryAccessToken($"{connection.ClientId}.{sealedPart}", (int)TokenLifetime.TotalSeconds);
    }

    /// <summary>The connection, on, of a customer that is active; null otherwise.</summary>
    private async Task<ProvisioningConnection?> UsableAsync(string clientId, CancellationToken cancellationToken)
    {
        var connection = await this.connections.FindByClientIdAsync(clientId, cancellationToken);
        if (connection is not { IsEnabled: true })
        {
            return null;
        }

        return await this.tenants.StatusOfAsync(connection.TenantId, cancellationToken) == TenantStatus.Active ? connection : null;
    }

    private bool Opens(string sealedPart, ProvisioningConnection connection)
    {
        try
        {
            // The time limit is checked by Unprotect itself; a token sealed for another secret no longer matches.
            return this.protector.Unprotect(sealedPart, out _) == Seal(connection);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    private static string Seal(ProvisioningConnection connection)
        => string.Create(CultureInfo.InvariantCulture, $"{connection.Id:N}|{connection.SecretIssuedAtUtc.UtcTicks}");
}
