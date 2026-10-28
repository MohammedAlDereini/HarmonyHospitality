using System.Buffers.Text;
using System.Security.Cryptography;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Signing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Infrastructure.IdentityServer.SignIn;

/// <summary>A sign-in waiting for the person to trust the browser: who, which browser, how they proved themselves.</summary>
public sealed record HeldNewDevice(User User, string KeyHash, string Method, string TrailAction, string? Detail, string Label);

/// <summary>The outcome of a "Trust device" link: the browser that is now trusted, for the page to say so.</summary>
public sealed record TrustedDevice(string Label);

/// <summary>
/// The Mews gate (later.md L16): when a customer requires trusted devices, a local sign-in from a browser the account
/// has not trusted goes on hold, proved but not signed in. An e-mail carries a one-time link; opening it marks the
/// browser trusted and the waiting page finishes the sign-in. The hold and the link live 15 minutes in Redis; a lost
/// mail keeps the person out, never lets a stranger in. People the company signs in are never gated here.
/// </summary>
public sealed class NewDeviceApproval
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>Identity's own page that trusts the browser; the mail's link points at it.</summary>
    public const string TrustPath = "/account/trustdevice";

    private const string HoldPrefix = "identity:new-device:hold:";
    private const string LinkPrefix = "identity:new-device:link:";
    private const int SecretLength = 43; // 32 bytes, base64url, no padding

    private readonly ICache<Harmony.Core.Cache.Providers.Redis> cache;
    private readonly IUserAccountRepository userAccounts;
    private readonly IUserDeviceRepository devices;
    private readonly ITenantRepository tenants;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly IAccountMailer mailer;
    private readonly TimeProvider clock;
    private readonly string issuer;
    private readonly ILogger<NewDeviceApproval> logger;

    public NewDeviceApproval(
        ICache<Harmony.Core.Cache.Providers.Redis> cache,
        IUserAccountRepository userAccounts,
        IUserDeviceRepository devices,
        ITenantRepository tenants,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        IAccountMailer mailer,
        TimeProvider clock,
        IOptions<TokenIssuerSettings> issuer,
        ILogger<NewDeviceApproval> logger)
    {
        this.cache = cache;
        this.userAccounts = userAccounts;
        this.devices = devices;
        this.tenants = tenants;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.mailer = mailer;
        this.clock = clock;
        this.issuer = issuer.Value.Issuer.TrimEnd('/');
        this.logger = logger;
    }

    /// <summary>True when this customer gates new devices and this browser is not trusted by this account.</summary>
    public async Task<bool> MustWaitAsync(User user, string deviceKey, CancellationToken cancellationToken)
    {
        var tenant = await this.tenants.FindAsync(user.TenantId, cancellationToken);
        if (tenant is not { TrustedDevicesRequired: true })
        {
            return false;
        }

        var device = await this.devices.FindByKeyAsync(user.Id, UserDevice.HashOf(deviceKey), cancellationToken);
        return device is not { IsTrusted: true };
    }

    /// <summary>Puts the proved sign-in on hold, writes the trail line, mails the link; returns the secret for the browser's cookie.</summary>
    public async Task<string> HoldAsync(User user, string deviceKey, string method, string trailAction, string? detail, CancellationToken cancellationToken)
    {
        var row = this.securityEvents.Create(user, SecurityAuditActions.SignInNewDeviceHeld, detail);
        await this.auditEvents.AddAsync(row, cancellationToken);
        await this.auditEvents.SaveChangesAsync(cancellationToken);

        var label = row.Device ?? "an unknown browser on an unknown system";
        var keyHash = UserDevice.HashOf(deviceKey);
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        await this.SaveAsync(HoldPrefix + secret, string.Join('|', user.Id.ToString("D"), keyHash, method, trailAction, detail ?? "", label), cancellationToken);
        await this.MailAsync(user, keyHash, label, row.SourceAddress, row.OccurredAtUtc, cancellationToken);

        this.logger.LogInformation("Sign-in on hold for account {UserId}: the browser must be trusted first.", user.Id);
        return secret;
    }

    /// <summary>The held sign-in behind this browser's secret, or null: unknown, expired or released.</summary>
    public async Task<HeldNewDevice?> FindAsync(string? secret, CancellationToken cancellationToken)
    {
        if (secret is null || secret.Length != SecretLength)
        {
            return null;
        }

        var response = await this.cache.GetAsync<string>(HoldPrefix + secret, eCacheAccessLevel.Public, cancellationToken);
        var parts = response?.Value?.Split('|', 6);
        if (parts is not { Length: 6 } || !Guid.TryParse(parts[0], out var userId))
        {
            return null;
        }

        var user = await this.userAccounts.FindByIdAcrossTenantsAsync(userId, cancellationToken);
        return user is null ? null : new HeldNewDevice(user, parts[1], parts[2], parts[3], parts[4].Length > 0 ? parts[4] : null, parts[5]);
    }

    public async Task<bool> IsTrustedAsync(HeldNewDevice held, CancellationToken cancellationToken)
    {
        var device = await this.devices.FindByKeyAsync(held.User.Id, held.KeyHash, cancellationToken);
        return device is { IsTrusted: true };
    }

    public Task ReleaseAsync(string secret, CancellationToken cancellationToken)
        => this.cache.DeleteAsync(HoldPrefix + secret, eCacheAccessLevel.Public, cancellationToken);

    /// <summary>A new link by mail for the same held sign-in (the first mail is late or lost).</summary>
    public Task ResendAsync(HeldNewDevice held, CancellationToken cancellationToken)
        => this.MailAsync(held.User, held.KeyHash, held.Label, null, this.clock.GetUtcNow(), cancellationToken);

    /// <summary>
    /// The link was opened: the browser is trusted from now on (made known to the account if the sign-in never finished
    /// before), the trail says so, and the link is spent. Null when the link is unknown, used or expired.
    /// </summary>
    public async Task<TrustedDevice?> TrustAsync(string? token, CancellationToken cancellationToken)
    {
        if (token is null || token.Length != SecretLength)
        {
            return null;
        }

        var response = await this.cache.GetAsync<string>(LinkPrefix + token, eCacheAccessLevel.Public, cancellationToken);
        var parts = response?.Value?.Split('|', 3);
        if (parts is not { Length: 3 } || !Guid.TryParse(parts[0], out var userId))
        {
            return null;
        }

        // Spent before anything else: a second open of the same link finds nothing.
        await this.cache.DeleteAsync(LinkPrefix + token, eCacheAccessLevel.Public, cancellationToken);

        var user = await this.userAccounts.FindByIdAcrossTenantsAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var now = this.clock.GetUtcNow();
        var device = await this.devices.FindByKeyAsync(user.Id, parts[1], cancellationToken);
        if (device is null)
        {
            device = UserDevice.First(user.TenantId, user.Id, parts[1], parts[2], null, null, now);
            await this.devices.AddAsync(device, cancellationToken);
        }

        device.Trust(now);
        await this.auditEvents.AddAsync(this.securityEvents.Create(user, SecurityAuditActions.DeviceTrusted, device.Label), cancellationToken);
        await this.devices.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Account {UserId} trusted a device ({Device}).", user.Id, device.Label);
        return new TrustedDevice(device.Label);
    }

    private async Task MailAsync(User user, string keyHash, string label, string? address, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        await this.SaveAsync(LinkPrefix + token, string.Join('|', user.Id.ToString("D"), keyHash, label), cancellationToken);
        await this.mailer.SendNewDeviceApprovalAsync(user, label, address, at, $"{this.issuer}{TrustPath}?token={token}", cancellationToken);
    }

    private async Task SaveAsync(string key, string value, CancellationToken cancellationToken)
    {
        var result = await this.cache.SaveAsync(key, value, Lifetime, eCacheAccessLevel.Public, cancellationToken);
        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException($"Storing the new-device hold failed ({result}).");
        }
    }
}
