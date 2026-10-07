using System.Security.Cryptography;
using System.Text;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// A browser an account has signed in from, like Mews's "Devices and activity". The browser carries a random key in a
/// cookie; only its SHA-256 is stored, so the table cannot be used to forge a device. One browser on a shared front-desk PC
/// is one row per account. The session id is the Identity sign-in session last opened there, so it can be ended alone.
/// </summary>
public sealed class UserDevice : BaseEntity<Guid>, IMultiTenantEntity
{
    public const int KeyHashLength = 64;
    public const int LabelMaxLength = 64;
    public const int AddressMaxLength = 45;
    public const int SessionIdMaxLength = 100;

    private UserDevice()
    {
    }

    private UserDevice(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserAccountId { get; private set; }

    /// <summary>SHA-256 of the browser's device key, 64 hex characters.</summary>
    public string KeyHash { get; private set; } = null!;

    /// <summary>"Chrome on Windows", from the User-Agent at the last sign-in.</summary>
    public string Label { get; private set; } = null!;

    public DateTimeOffset FirstSeenUtc { get; private set; }

    public DateTimeOffset LastSeenUtc { get; private set; }

    public string? LastAddress { get; private set; }

    /// <summary>The Identity sign-in session last opened on this device; null when none is known.</summary>
    public string? SessionId { get; private set; }

    public static UserDevice First(Guid tenantId, Guid userAccountId, string keyHash, string label, string? address, string? sessionId, DateTimeOffset at)
    {
        if (userAccountId == Guid.Empty)
        {
            throw new ArgumentException("The account is required.", nameof(userAccountId));
        }

        if (keyHash is not { Length: KeyHashLength })
        {
            throw new ArgumentException("A 64-character SHA-256 is required.", nameof(keyHash));
        }

        var device = new UserDevice(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            UserAccountId = userAccountId,
            KeyHash = keyHash,
            FirstSeenUtc = at,
        };
        device.SeenAgain(label, address, sessionId, at);
        return device;
    }

    /// <summary>What is stored for a device key: its SHA-256, 64 hex characters.</summary>
    public static string HashOf(string deviceKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(deviceKey)));

    /// <summary>Another sign-in from this browser: what it looks like, where it came from, and its new session.</summary>
    public void SeenAgain(string label, string? address, string? sessionId, DateTimeOffset at)
    {
        Label = string.IsNullOrWhiteSpace(label) ? "Unknown browser" : label.Length > LabelMaxLength ? label[..LabelMaxLength] : label;
        LastAddress = address is { Length: > AddressMaxLength } ? address[..AddressMaxLength] : address;
        SessionId = sessionId is { Length: > SessionIdMaxLength } ? null : sessionId;
        LastSeenUtc = at;
    }
}
