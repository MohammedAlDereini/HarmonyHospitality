namespace Harmony.Web.Bff.Sessions;

using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Core.Models;
using Providers = Harmony.Core.Cache.Providers;
using Harmony.Web.Bff.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

/// <summary>
/// The session lives HERE, not in the browser: the cookie carries only this store's key, so it stays small, and
/// deleting a row here ends the session instantly everywhere — the kill switch the whole design leans on
/// (RFC 10017 §6.1: the BFF SHOULD encrypt stored session content — every ticket is sealed by Data Protection
/// before Redis sees it). A second index maps Identity's sid to the session, for back-channel logout.
/// </summary>
internal sealed class RedisTicketStore : ITicketStore
{
    internal const string SessionPrefix = "session:";
    internal const string SidPrefix = "session-by-sid:";

    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory scopes;
    private readonly IDataProtector protector;
    private readonly TimeSpan idle;
    private readonly string prefix;
    private readonly string sidPrefix;

    public RedisTicketStore(IServiceScopeFactory scopes, IDataProtectionProvider dataProtection, AppInfo appInfo, IOptions<BffSettings> options)
    {
        this.scopes = scopes;
        this.protector = dataProtection.CreateProtector("Harmony.Web.Bff.SessionTickets");
        this.idle = TimeSpan.FromMinutes(options.Value.SessionIdleMinutes);
        this.prefix = $"{appInfo.Name.ToLowerInvariant()}:{SessionPrefix}";
        this.sidPrefix = $"{appInfo.Name.ToLowerInvariant()}:{SidPrefix}";
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Guid.CreateVersion7().ToString("N");
        await this.WriteAsync(key, ticket);

        // Identity's session id points at ours, so a back-channel logout can kill exactly this session.
        if (ticket.Principal.FindFirst("sid")?.Value is string sid)
        {
            using var scope = this.scopes.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICache<Providers.Redis>>();
            _ = await cache.SaveAsync(this.sidPrefix + sid, key, this.LifetimeOf(ticket), eCacheAccessLevel.Public, CancellationToken.None);
        }

        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) => this.WriteAsync(key, ticket);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        using var scope = this.scopes.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICache<Providers.Redis>>();
        var stored = await cache.GetAsync<string>(this.prefix + key, eCacheAccessLevel.Public, CancellationToken.None);
        if (stored.CacheOperationResult != eCacheOperationResult.Success || string.IsNullOrEmpty(stored.Value))
        {
            return null;
        }

        try
        {
            return TicketSerializer.Default.Deserialize(this.protector.Unprotect(Convert.FromBase64String(stored.Value)));
        }
        catch (Exception)
        {
            // A ticket this host cannot open (rotated-away key, tampering) is simply no session.
            return null;
        }
    }

    public async Task RemoveAsync(string key)
    {
        using var scope = this.scopes.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICache<Providers.Redis>>();

        // The sid index goes with the session, or a later logout would chase a ghost.
        var ticket = await this.RetrieveAsync(key);
        if (ticket?.Principal.FindFirst("sid")?.Value is string sid)
        {
            _ = await cache.DeleteAsync(this.sidPrefix + sid, eCacheAccessLevel.Public, CancellationToken.None);
        }

        _ = await cache.DeleteAsync(this.prefix + key, eCacheAccessLevel.Public, CancellationToken.None);
    }

    /// <summary>The session key Identity's sid points at, or null. The back-channel logout endpoint asks this.</summary>
    public async Task<string?> SessionKeyBySidAsync(string sid, CancellationToken cancellationToken)
    {
        using var scope = this.scopes.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICache<Providers.Redis>>();
        var found = await cache.GetAsync<string>(this.sidPrefix + sid, eCacheAccessLevel.Public, cancellationToken);
        return found.CacheOperationResult == eCacheOperationResult.Success ? found.Value : null;
    }

    private async Task WriteAsync(string key, AuthenticationTicket ticket)
    {
        var sealedTicket = Convert.ToBase64String(this.protector.Protect(TicketSerializer.Default.Serialize(ticket)));
        var life = this.LifetimeOf(ticket);

        using var scope = this.scopes.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICache<Providers.Redis>>();
        _ = await cache.SaveAsync(this.prefix + key, sealedTicket, life, eCacheAccessLevel.Public, CancellationToken.None);

        // Identity's session id points at ours, so a back-channel logout can end exactly this session. Written
        // on EVERY renewal, not only at sign-in: an index left at its sign-in lifetime would expire before a
        // long shift's session does, and the logout would then find nothing to end.
        if (ticket.Principal.FindFirst("sid")?.Value is string sid)
        {
            _ = await cache.SaveAsync(this.sidPrefix + sid, key, life, eCacheAccessLevel.Public, CancellationToken.None);
        }
    }

    /// <summary>Redis expires what the cookie outlives: the ticket's own end plus a little grace, never forever.</summary>
    private TimeSpan LifetimeOf(AuthenticationTicket ticket)
    {
        var life = ticket.Properties.ExpiresUtc is DateTimeOffset expires
            ? expires - DateTimeOffset.UtcNow
            : this.idle;

        return (life > TimeSpan.Zero ? life : this.idle) + Grace;
    }
}
