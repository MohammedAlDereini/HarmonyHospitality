namespace Harmony.Web.Bff.Authentication;

using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Web.Bff.Sessions;
using Harmony.Web.Bff.Settings;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Providers = Harmony.Core.Cache.Providers;

/// <summary>
/// The RELIABLE kill: Identity POSTS here, server to server, when a session ends on its side — logout, suspend,
/// sign-out-everywhere. No browser, no iframe, no third-party-cookie lottery. The logout_token is believed only
/// after everything the Back-Channel Logout 1.0 spec demands: Identity's signature, issuer, audience, the
/// logout+jwt type, a fresh lifetime, the backchannel-logout event claim, NO nonce, an unseen jti, and the sid.
/// Then the sid index (kept since the session's birth) names exactly which session dies.
/// </summary>
internal static class BackChannelLogout
{
    internal const string Path = "/bff/backchannel-logout";

    private const string EventName = "http://schemas.openid.net/event/backchannel-logout";
    private static readonly TimeSpan ReplayMemory = TimeSpan.FromMinutes(10);

    public static async Task<IResult> HandleAsync(
        HttpContext http,
        RedisTicketStore sessions,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        IOptions<BffSettings> bff,
        IServiceScopeFactory scopes,
        ILogger<RedisTicketStore> logger)
    {
        http.Response.Headers.CacheControl = "no-store";

        var logoutToken = http.Request.HasFormContentType ? (await http.Request.ReadFormAsync(http.RequestAborted))["logout_token"].ToString() : string.Empty;
        if (string.IsNullOrEmpty(logoutToken))
        {
            return Results.BadRequest();
        }

        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var configuration = await options.ConfigurationManager!.GetConfigurationAsync(http.RequestAborted);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(logoutToken, new TokenValidationParameters
        {
            ValidIssuer = configuration.Issuer,
            ValidAudience = bff.Value.ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidTypes = ["logout+jwt"],
        });

        var sid = result.IsValid ? result.ClaimsIdentity.FindFirst("sid")?.Value : null;
        var jti = result.IsValid ? result.ClaimsIdentity.FindFirst("jti")?.Value : null;
        var events = result.IsValid ? result.ClaimsIdentity.FindFirst("events")?.Value : null;
        if (!result.IsValid
            || string.IsNullOrEmpty(sid)
            || string.IsNullOrEmpty(jti)
            || events?.Contains(EventName, StringComparison.Ordinal) != true
            || result.ClaimsIdentity.FindFirst("nonce") is not null)
        {
            logger.LogWarning("A back-channel logout token was refused (valid={Valid}).", result.IsValid);
            return Results.BadRequest();
        }

        // A replayed token is refused: each jti works exactly once within its short life.
        using (var scope = scopes.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<ICache<Providers.Redis>>();
            if (await cache.AddAsync("webbff:logout-jti:" + jti, "1", ReplayMemory, eCacheAccessLevel.Public, http.RequestAborted) == eCacheOperationResult.KeyAlreadyExists)
            {
                return Results.BadRequest();
            }
        }

        // Already gone is still a success: the spec's logout is idempotent.
        if (await sessions.SessionKeyBySidAsync(sid, http.RequestAborted) is string sessionKey)
        {
            await sessions.RemoveAsync(sessionKey);
            logger.LogInformation("A back-channel logout ended the session of sid {Sid}.", sid);
        }

        return Results.Ok();
    }
}
