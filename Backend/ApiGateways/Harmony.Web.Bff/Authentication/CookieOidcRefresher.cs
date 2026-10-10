namespace Harmony.Web.Bff.Authentication;

using System.Globalization;
using System.Security.Claims;
using Harmony.Core.Abstractions;
using Harmony.Web.Bff.Sessions;
using Harmony.Web.Bff.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Keeps the session's tokens fresh without the person noticing (the shape of Microsoft's own CookieOidcRefresher):
/// when the access token nears its end, the refresh token is spent at Identity — under a distributed lock, because
/// refresh tokens are one-time-use and two parallel requests must never both spend one. The id_token that comes back
/// is VALIDATED against Identity's published keys before its claims replace the session's. A refused refresh
/// (the account was suspended, signed out everywhere, or the token was already spent elsewhere) ends the session
/// on the spot. It also enforces the session's absolute cap: no cookie outlives its hard end, however active.
/// </summary>
internal sealed class CookieOidcRefresher
{
    internal const string SessionStartedKey = "harmony.session-started";
    internal const string BackchannelClientName = "oidc-backchannel";

    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory scopes;
    private readonly IHttpClientFactory httpClients;
    private readonly IOptionsMonitor<OpenIdConnectOptions> oidcOptions;
    private readonly ClientAssertionFactory assertions;
    private readonly RedisTicketStore store;
    private readonly BffSettings settings;
    private readonly ILogger<CookieOidcRefresher> logger;

    public CookieOidcRefresher(IServiceScopeFactory scopes, IHttpClientFactory httpClients, IOptionsMonitor<OpenIdConnectOptions> oidcOptions, ClientAssertionFactory assertions, RedisTicketStore store, IOptions<BffSettings> options, ILogger<CookieOidcRefresher> logger)
    {
        this.scopes = scopes;
        this.httpClients = httpClients;
        this.oidcOptions = oidcOptions;
        this.assertions = assertions;
        this.store = store;
        this.settings = options.Value;
        this.logger = logger;
    }

    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        // The absolute cap first: an eight-hour shift's cookie dies at hour eight, however busy the desk is.
        if (DateTimeOffset.TryParse(context.Properties.GetString(SessionStartedKey), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var started)
            && DateTimeOffset.UtcNow - started > TimeSpan.FromHours(this.settings.SessionAbsoluteHours))
        {
            this.logger.LogInformation("A session reached its absolute cap of {Hours} hours and ended.", this.settings.SessionAbsoluteHours);
            await EndSessionAsync(context);
            return;
        }

        // Fresh enough: nothing to do (the common case on almost every request).
        if (!DateTimeOffset.TryParse(context.Properties.GetTokenValue("expires_at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)
            || DateTimeOffset.UtcNow + Skew < expiresAt)
        {
            return;
        }

        var refreshToken = context.Properties.GetTokenValue("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            // No way to continue the session silently: it ends when the access token does.
            return;
        }

        // One refresh per session at a time, across every instance: refresh tokens are one-time-use, and the
        // loser of a doubled request must WAIT and walk on, never spend the same token again.
        // The framework registers the lock PROVIDER-TYPED (IDistributedLockManager&lt;Redis&gt;, the same way its own
        // tests resolve it); the bare interface is never in the container — s41's live fire caught exactly that.
        var lockKey = $"refresh:{context.Principal?.FindFirstValue("sub")}:{context.Principal?.FindFirstValue("sid")}";
        using var scope = this.scopes.CreateScope();
        var locks = scope.ServiceProvider.GetRequiredService<IDistributedLockManager<Harmony.Core.Cache.Providers.Redis>>();

        var first = await locks.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30), privateCache: false);
        if (!first)
        {
            for (var wait = 0; wait < 20 && !await locks.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30), privateCache: false); wait++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            // The winner refreshed while we waited: this request rides on with what the WINNER stored, never
            // its own stale copy.
            _ = await this.AdoptStoredAsync(context);
            _ = await locks.UnlockAsync(lockKey, privateCache: false);
            return;
        }

        try
        {
            // Check AFTER taking the lock: a request that loaded its session a moment before another one
            // rotated the token, and only then won the lock, holds a spent copy — spending it would end the
            // session for everyone. If the store already moved on, adopt its tokens and refresh nothing.
            if (await this.AdoptStoredAsync(context))
            {
                return;
            }

            await this.RefreshAsync(context, refreshToken);
        }
        finally
        {
            _ = await locks.UnlockAsync(lockKey, privateCache: false);
        }
    }

    /// <summary>The store's key for this session, through the sid index the store keeps for back-channel logout.</summary>
    private async Task<string?> SessionKeyAsync(CookieValidatePrincipalContext context)
    {
        var sid = context.Principal?.FindFirstValue("sid");
        return sid is null ? null : await this.store.SessionKeyBySidAsync(sid, context.HttpContext.RequestAborted);
    }

    /// <summary>
    /// When the stored session already carries a NEWER refresh token than this request loaded, take the stored
    /// principal and tokens and report true; false means this request's copy is the current one.
    /// </summary>
    private async Task<bool> AdoptStoredAsync(CookieValidatePrincipalContext context)
    {
        var key = await this.SessionKeyAsync(context);
        var stored = key is null ? null : await this.store.RetrieveAsync(key);
        var storedRefreshToken = stored?.Properties.GetTokenValue("refresh_token");
        if (stored is null || string.IsNullOrEmpty(storedRefreshToken) || storedRefreshToken == context.Properties.GetTokenValue("refresh_token"))
        {
            return false;
        }

        context.ReplacePrincipal(stored.Principal);
        foreach (var name in new[] { "access_token", "refresh_token", "id_token", "expires_at" })
        {
            if (stored.Properties.GetTokenValue(name) is string value)
            {
                _ = context.Properties.UpdateTokenValue(name, value);
            }
        }

        return true;
    }

    /// <summary>
    /// The renewed session goes to the store NOW, while the lock is still held. The cookie handler writes it
    /// again at response time (ShouldRenew), but that moment comes after the lock is released — too late for
    /// the next request in line, which must already see the rotated token when it takes the lock.
    /// </summary>
    private async Task PersistAsync(CookieValidatePrincipalContext context)
    {
        var key = await this.SessionKeyAsync(context);
        if (key is not null)
        {
            await this.store.RenewAsync(key, new AuthenticationTicket(context.Principal!, context.Properties, context.Scheme.Name));
        }
    }

    private async Task RefreshAsync(CookieValidatePrincipalContext context, string refreshToken)
    {
        var options = this.oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var configuration = await options.ConfigurationManager!.GetConfigurationAsync(context.HttpContext.RequestAborted);

        using var response = await this.httpClients.CreateClient(BackchannelClientName).PostAsync(
            configuration.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = this.settings.ClientId,
                ["client_assertion_type"] = ClientAssertionFactory.AssertionType,
                ["client_assertion"] = this.assertions.Create(this.settings.Authority),
            }),
            context.HttpContext.RequestAborted);

        if (!response.IsSuccessStatusCode)
        {
            // The refresh token is dead: suspended, signed out everywhere, or spent by someone else. So is the session.
            this.logger.LogInformation("A session's refresh was refused ({Status}); the session ends.", (int)response.StatusCode);
            await EndSessionAsync(context);
            return;
        }

        var payload = await response.Content.ReadFromJsonAsync<RefreshAnswer>(context.HttpContext.RequestAborted);
        if (payload?.AccessToken is not { Length: > 0 })
        {
            this.logger.LogWarning("A refresh answered without an access token; the session rides out its current one.");
            return;
        }

        // The id_token's claims only count once Identity's own signature says so (Microsoft's refresher does the same).
        if (payload.IdToken is { Length: > 0 })
        {
            var result = await new JsonWebTokenHandler().ValidateTokenAsync(payload.IdToken, new TokenValidationParameters
            {
                ValidIssuer = configuration.Issuer,
                ValidAudience = this.settings.ClientId,
                IssuerSigningKeys = configuration.SigningKeys,
                NameClaimType = "name",
                RoleClaimType = "role",
            });

            if (!result.IsValid)
            {
                this.logger.LogWarning("A refresh answered with an id_token that did not validate; the session ends.");
                await EndSessionAsync(context);
                return;
            }

            // OpenID Connect Core §12.2: a refreshed id_token MUST speak of the SAME person. A different sub is
            // not a quirk, it is an account-swap — the session ends before any token or claim is touched.
            var refreshedSub = result.ClaimsIdentity.FindFirst("sub")?.Value;
            if (!string.Equals(refreshedSub, context.Principal?.FindFirstValue("sub"), StringComparison.Ordinal))
            {
                this.logger.LogWarning("A refresh answered with an id_token for a DIFFERENT subject; the session ends.");
                await EndSessionAsync(context);
                return;
            }

            context.ReplacePrincipal(new ClaimsPrincipal(result.ClaimsIdentity));
            _ = context.Properties.UpdateTokenValue("id_token", payload.IdToken);
        }

        _ = context.Properties.UpdateTokenValue("access_token", payload.AccessToken);
        if (payload.RefreshToken is { Length: > 0 })
        {
            _ = context.Properties.UpdateTokenValue("refresh_token", payload.RefreshToken);
        }

        _ = context.Properties.UpdateTokenValue("expires_at", DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn).ToString("o", CultureInfo.InvariantCulture));
        await this.PersistAsync(context);
        context.ShouldRenew = true;
    }

    /// <summary>
    /// Ending a session here means BOTH: reject the principal for this request, and sign the cookie scheme out —
    /// which is what removes the stored session row (Microsoft's refresher pairs them the same way). Reject alone
    /// leaves a dead row behind until its own timer; s41 measured exactly that.
    /// </summary>
    private static async Task EndSessionAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private sealed record RefreshAnswer
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
