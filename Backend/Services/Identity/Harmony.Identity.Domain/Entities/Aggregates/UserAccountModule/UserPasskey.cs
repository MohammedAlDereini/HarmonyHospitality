using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// A passkey (a WebAuthn credential) an account signs in with, like Mews's passkeys: the PUBLIC half of a key pair the
/// person's device or password manager holds. The private half never exists here, so a copy of this table signs nobody
/// in. One row per credential. A synced ("backed up") passkey lives in the person's Apple, Google or password-manager
/// account and follows them to their other devices; a device-bound one (a security key) never leaves the device.
/// </summary>
public sealed class UserPasskey : BaseEntity<Guid>, IMultiTenantEntity
{
    /// <summary>WebAuthn allows credential ids of up to 1023 bytes.</summary>
    public const int CredentialIdMaxLength = 1023;
    public const int NameMaxLength = 64;
    public const int TransportsMaxLength = 128;

    /// <summary>Passkeys an account may hold at once.</summary>
    public const int MaxPerAccount = 10;

    private UserPasskey()
    {
    }

    private UserPasskey(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserAccountId { get; private set; }

    /// <summary>The credential id the authenticator chose; unique across every account.</summary>
    public byte[] CredentialId { get; private set; } = null!;

    /// <summary>The public key, COSE-encoded as the authenticator gave it.</summary>
    public byte[] PublicKey { get; private set; } = null!;

    /// <summary>What the person called it: "Work laptop", "YubiKey".</summary>
    public string Name { get; private set; } = null!;

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset? LastUsedUtc { get; private set; }

    /// <summary>The authenticator's signature counter at the last use; stays 0 for authenticators that keep none (synced passkeys).</summary>
    public long SignCount { get; private set; }

    /// <summary>How the authenticator reaches the browser ("internal", "hybrid", "usb"...), comma-separated; null when it did not say.</summary>
    public string? Transports { get; private set; }

    /// <summary>The person verified themselves (biometric or PIN) when the passkey was created.</summary>
    public bool IsUserVerified { get; private set; }

    /// <summary>The authenticator can sync this passkey to the person's other devices.</summary>
    public bool IsBackupEligible { get; private set; }

    /// <summary>The passkey is currently synced (backed up) to the person's other devices.</summary>
    public bool IsBackedUp { get; private set; }

    /// <summary>The attestation object from creation, kept for a later audit; never read at sign-in.</summary>
    public byte[] AttestationObject { get; private set; } = null!;

    /// <summary>The client data from creation (origin, challenge), kept with the attestation.</summary>
    public byte[] ClientDataJson { get; private set; } = null!;

    public static UserPasskey Register(
        Guid tenantId,
        Guid userAccountId,
        string name,
        byte[] credentialId,
        byte[] publicKey,
        long signCount,
        string[]? transports,
        bool isUserVerified,
        bool isBackupEligible,
        bool isBackedUp,
        byte[] attestationObject,
        byte[] clientDataJson,
        DateTimeOffset at)
    {
        if (userAccountId == Guid.Empty)
        {
            throw new ArgumentException("The account is required.", nameof(userAccountId));
        }

        if (credentialId is not { Length: > 0 and <= CredentialIdMaxLength })
        {
            throw new ArgumentException($"A credential id of 1 to {CredentialIdMaxLength} bytes is required.", nameof(credentialId));
        }

        if (publicKey is not { Length: > 0 })
        {
            throw new ArgumentException("The public key is required.", nameof(publicKey));
        }

        var joined = transports is { Length: > 0 } ? string.Join(',', transports) : null;

        return new UserPasskey(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            UserAccountId = userAccountId,
            Name = NameOf(name),
            CredentialId = credentialId,
            PublicKey = publicKey,
            SignCount = signCount,
            Transports = joined is { Length: > TransportsMaxLength } ? joined[..TransportsMaxLength] : joined,
            IsUserVerified = isUserVerified,
            IsBackupEligible = isBackupEligible,
            IsBackedUp = isBackedUp,
            AttestationObject = attestationObject ?? [],
            ClientDataJson = clientDataJson ?? [],
            CreatedUtc = at,
        };
    }

    /// <summary>A name of 1 to 64 characters, trimmed. The command validates the input; a bad value here is a bug.</summary>
    public static string NameOf(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException($"A name of 1 to {NameMaxLength} characters is required.", nameof(value));
        }

        return trimmed;
    }

    /// <summary>A sign-in with this passkey: the counter and the backup state the authenticator reported now.</summary>
    public void Used(long signCount, bool isBackedUp, DateTimeOffset at)
    {
        SignCount = signCount;
        IsBackedUp = isBackedUp;
        LastUsedUtc = at;
    }

    /// <summary>The authenticator's state as Identity's store reports it, without counting a use.</summary>
    public void Updated(long signCount, bool isBackedUp)
    {
        SignCount = signCount;
        IsBackedUp = isBackedUp;
    }

    /// <summary>The person calls it something else ("Old laptop"); nothing about the key changes.</summary>
    public void Rename(string name)
    {
        Name = NameOf(name);
    }
}
