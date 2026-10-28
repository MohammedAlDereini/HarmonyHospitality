using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;

/// <summary>
/// A customer's door for its company directory (stage E, the ERD's ProvisioningConnections): Microsoft Entra ID or Okta
/// pushes staff and groups to Harmony over SCIM 2.0. One per customer, like Mews' SCIM page. The directory proves itself
/// with the client id and the secret; only the secret's SHA-256 is kept. A new secret kills the old one at once, and every
/// token made from it. Switched off, the directory is refused, but its people stay read-only in Harmony until the
/// connection itself is removed.
/// </summary>
public sealed class ProvisioningConnection : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int ClientIdLength = 32;

    public const int SecretHashLength = 32;

    private ProvisioningConnection()
    {
    }

    private ProvisioningConnection(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>The directory's name for itself at our token door: 32 random hex characters, not a secret.</summary>
    public string ClientId { get; private set; } = null!;

    /// <summary>SHA-256 of the secret. The secret is shown once, when made, and never kept.</summary>
    public byte[] SecretHash { get; private set; } = null!;

    /// <summary>When the current secret was made; a token made before it is dead.</summary>
    public DateTimeOffset SecretIssuedAtUtc { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    /// <summary>A new connection, on, with its first secret.</summary>
    public static ProvisioningConnection Open(Guid tenantId, string clientId, byte[] secretHash, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A customer is required.", nameof(tenantId));
        }

        if (clientId is not { Length: ClientIdLength })
        {
            throw new ArgumentException($"A client id is {ClientIdLength} characters.", nameof(clientId));
        }

        var connection = new ProvisioningConnection(Guid.CreateVersion7()) { TenantId = tenantId, ClientId = clientId, IsEnabled = true };
        connection.NewSecret(secretHash, now);
        return connection;
    }

    /// <summary>The old secret, and every token made from it, stop working now.</summary>
    public void NewSecret(byte[] secretHash, DateTimeOffset now)
    {
        if (secretHash is not { Length: SecretHashLength })
        {
            throw new ArgumentException($"A secret hash is {SecretHashLength} bytes.", nameof(secretHash));
        }

        SecretHash = secretHash;
        SecretIssuedAtUtc = now.ToUniversalTime();
    }

    public void SwitchOn() => IsEnabled = true;

    public void SwitchOff() => IsEnabled = false;

    /// <summary>The secret matches, compared in constant time.</summary>
    public bool Accepts(string secret)
        => !string.IsNullOrEmpty(secret) && CryptographicOperations.FixedTimeEquals(SecretHash, HashOf(secret));

    /// <summary>32 random bytes as hex: the client id.</summary>
    public static string NewClientId() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(ClientIdLength / 2));

    /// <summary>32 random bytes, URL-safe: the secret, shown once.</summary>
    public static string NewSecretValue() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static byte[] HashOf(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));
}
