using System.Net.Http.Headers;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Harmony.Core.DI;
using Harmony.Web.Bff.ApiDocs;
using Harmony.Web.Bff.Authentication;
using Harmony.Web.Bff.Pages;
using Harmony.Web.Bff.Security;
using Harmony.Web.Bff.Sessions;
using Harmony.Web.Bff.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using StackExchange.Redis;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// The framework's one-file settings: applicationSettings(.Environment).json from the run folder, like every service.
builder.Configuration.ConfigureCoreConfiguration(builder.Environment);

// Nothing tells a caller which server software to aim at.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

builder.Services.AddOptions<BffSettings>()
    .Bind(builder.Configuration.GetSection(BffSettings.Section))
    .Validate(BffSettings.IsValid, $"{BffSettings.Section}: Authority and ApiDestination must be absolute https URLs, Name letters and digits 2 to 32, the private key file must exist, the cookie must keep its __Host- prefix, SessionIdleMinutes 5 to 720, SessionAbsoluteHours 1 to 24, PasskeyEnrollUrl and PasskeyManageUrl both absolute https or both empty, CspReportsPerMinute at least 1, HstsMaxAgeDays 1 to 730, Languages two-letter lower-case codes with DefaultLanguage among them, every TrustedProxyAddresses entry an IP address.")
    .ValidateOnStart();

// The one API-docs page (/docs) for platform people and the services it lists. No section = no page.
builder.Services.AddOptions<ApiDocsSettings>()
    .Bind(builder.Configuration.GetSection(ApiDocsSettings.Section))
    .Validate(ApiDocsSettings.IsValid, $"{ApiDocsSettings.Section}: every service needs a short Name (letters, digits, dashes) unique on the list and an absolute https Url; UserName and PasswordPath both set (file must exist) or both empty.")
    .ValidateOnStart();
builder.Services.AddHttpClient(ApiDocsPage.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10)).ConfigurePrimaryHttpMessageHandler(() =>
    builder.Environment.IsDevelopment()
        ? new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }
        : new HttpClientHandler());

// The front door hides the caller: it writes the real address and scheme into X-Forwarded-For / -Proto. We believe
// those headers only from the proxies listed in TrustedProxyAddresses. Microsoft trusts this machine by default and, with
// BOTH lists empty, every sender; here an empty list means the headers are not read at all (trust nobody).
builder.Services.AddOptions<ForwardedHeadersOptions>()
    .Configure<IOptions<BffSettings>>((options, bff) =>
    {
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        if (bff.Value.TrustedProxyAddresses.Length == 0)
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var address in bff.Value.TrustedProxyAddresses)
        {
            options.KnownProxies.Add(IPAddress.Parse(address));
        }
    });

// The shared fabric this host stands on: the framework core (AppInfo names every Redis key after this host, the call
// context the lock manager leans on), the Redis cache that holds the sessions, and the distributed lock that
// makes token refresh single-flight. The name comes from settings: the console host runs this same code under its
// own name, so its keys, its key ring and its tickets never meet the web host's.
var hostSettings = builder.Configuration.GetSection(BffSettings.Section).Get<BffSettings>() ?? new BffSettings();
Harmony.Core.DI.DependenciesConfigurator.AddCore(builder.Services, builder.Configuration, core =>
    core.AddAppInfo(app =>
    {
        app.Name = hostSettings.Name;
        app.Title = hostSettings.DataProtectionApplicationName;
        app.Description = $"Harmony {hostSettings.Name}";
    }));
Harmony.Core.Cache.DI.DependenciesConfigurator.AddCache(builder.Services, builder.Configuration, cache =>
{
    cache.AddRedisCache();
    cache.AddRedisDistributedLock();
});

// The key ring lives in Redis (restarts and sibling instances share it) and only this certificate opens it;
// the application name keeps this host's keys apart from every other service for good.
var dataProtectionSettings = builder.Configuration.GetSection(DataProtectionSettings.Section).Get<DataProtectionSettings>() ?? new DataProtectionSettings();
if (!DataProtectionSettings.IsValid(dataProtectionSettings))
{
    throw new InvalidOperationException($"{DataProtectionSettings.Section}: CertificatePath must name an existing PEM file holding the certificate and its key.");
}

builder.Services.AddDataProtection()
    .SetApplicationName(hostSettings.DataProtectionApplicationName)
    .PersistKeysToStackExchangeRedis(ConnectionMultiplexer.Connect(RedisConnection(builder.Configuration)), hostSettings.KeyRingKey)
    .ProtectKeysWithCertificate(X509Certificate2.CreateFromPemFile(dataProtectionSettings.CertificatePath));

builder.Services.AddSingleton<ClientAssertionFactory>();
builder.Services.AddSingleton<RedisTicketStore>();
builder.Services.AddSingleton<CookieOidcRefresher>();
ConfigureAuthentication(builder);

// The session body lives in Redis and the cookie carries only its key; every request passes the refresher:
// the absolute cap first, then a silent token refresh when the access token nears its end.
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<RedisTicketStore, CookieOidcRefresher>((options, store, refresher) =>
    {
        options.SessionStore = store;
        options.Events.OnValidatePrincipal = refresher.ValidateAsync;
    });

builder.Services.AddHttpClient(CookieOidcRefresher.BackchannelClientName).ConfigurePrimaryHttpMessageHandler(() =>
    builder.Environment.IsDevelopment()
        ? new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }
        : new HttpClientHandler());

// Deny by default: an endpoint added tomorrow without a thought is BORN protected; the public doors below
// say AllowAnonymous out loud.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// The ONE proxied door: /api/{**} to the platform API, nothing else. The route says "anonymous" OUT LOUD
// because proxy routes fall under the fallback policy like every endpoint — without it, deny-by-default
// would 401 the API's anonymous doors before the API could answer. The API stays the authority:
// protected doors demand the Bearer token this BFF attaches only when a session exists.
var bffSettings = builder.Configuration.GetSection(BffSettings.Section).Get<BffSettings>() ?? new BffSettings();

// The report door's limiter: ASP.NET Core's own, one fixed window per client address (the real one: forwarded
// headers run first). A flood answers 429 and is never logged.
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(CspViolationReports.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = bffSettings.CspReportsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddReverseProxy()
    .LoadFromMemory(
        [
            new RouteConfig
            {
                RouteId = "api",
                ClusterId = "api",
                Match = new RouteMatch { Path = "/api/{**rest}" },
                AuthorizationPolicy = "anonymous",
            },
        ],
        [
            new ClusterConfig
            {
                ClusterId = "api",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["api"] = new() { Address = bffSettings.ApiDestination },
                },
                HttpClient = builder.Environment.IsDevelopment()
                    // Development only: the local dev certificate; production keeps full validation.
                    ? new HttpClientConfig { DangerousAcceptAnyServerCertificate = true }
                    : null,
            },
        ])
    .AddTransforms(transforms =>
    {
        // X-Forwarded-For REPLACED, never appended: a caller writing its own header lies to nobody,
        // and the trail's SourceAddress downstream records the truth.
        transforms.AddXForwarded(ForwardedTransformActions.Set);

        // The session cookie and the CSRF header are OUR protocol with the browser; the API never sees them.
        transforms.AddRequestHeaderRemove("Cookie");
        transforms.AddRequestHeaderRemove(Harmony.Web.Bff.Security.CsrfHeaderMiddleware.HeaderName);

        // A caller's own Authorization header never passes: the only token the API sees from this door is the
        // session's, attached below. Without a session the call reaches the API with no token at all.
        transforms.AddRequestHeaderRemove("Authorization");

        // The whole point of the BFF: the browser holds a cookie, the API receives a Bearer token — attached
        // only when a session exists, already refreshed by the cookie refresher on this very request.
        transforms.AddRequestTransform(async context =>
        {
            var accessToken = await context.HttpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        });
    });

// HSTS for the days the setting says, subdomains included (OWASP: up to two years; Microsoft: start low on a new
// domain, raise once HTTPS is proven). Preload stays off until the apex is submitted at hstspreload.org. The front
// door must say the same or nothing: YARP keeps the proxy's value over the destination's. Loopback hosts stay
// excluded (the framework's default), so a developer's browser never pins localhost.
builder.Services.AddHsts(hsts =>
{
    hsts.MaxAge = TimeSpan.FromDays(bffSettings.HstsMaxAgeDays);
    hsts.IncludeSubDomains = true;
});

var app = builder.Build();

// First of all middleware (Microsoft: before UseHsts): from here on the request carries the caller's real address
// and scheme when it came through a listed proxy, so HSTS, the proxy transform and the OIDC redirects see the truth.
app.UseForwardedHeaders();

// Outside Development the browser is told: HTTPS only for this host, from the first answer on.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Armor first (no path can forget its headers), then the CSRF wall, then who-you-are.
app.UseMiddleware<Harmony.Web.Bff.Security.SecurityHeadersMiddleware>();
app.UseMiddleware<Harmony.Web.Bff.Security.CsrfHeaderMiddleware>();

// Production: the built web app lives in wwwroot, one folder per language (wwwroot/en, wwwroot/ar). Its files are
// served BEFORE authentication runs — a stylesheet needs no session and must not spend a token refresh. The hashed
// assets may be cached forever. The page itself (index.html) is never a file: it is rendered per request with its
// nonce from the chosen language's folder (WebAppPage, the fallback below).
if (bffSettings.ServeStaticFiles)
{
    app.Use((context, next) =>
    {
        // The page is rendered, never served as a file: /index.html and a language's /ar, /ar/, /ar/index.html are
        // the page too (in that language), not the raw file with the marker in it.
        var path = context.Request.Path;

        // ?lang=ar on a page: remember it in the cookie and come back to the same address without it (D7: URLs stay
        // identical in both languages; the cookie carries the choice until the profile does, D26).
        if (!DoorPaths.IsDoor(path) && context.Request.Query.TryGetValue(PageLanguage.Query, out var wanted) && bffSettings.Languages.Contains(wanted.ToString(), StringComparer.Ordinal))
        {
            PageLanguage.Remember(context, wanted.ToString());
            var rest = QueryString.Create(context.Request.Query.Where(q => q.Key != PageLanguage.Query).SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v))));
            context.Response.Redirect(path + rest);
            return Task.CompletedTask;
        }

        if (path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Path = "/";
        }
        else if (PageLanguage.IsLocaleIndex(path, bffSettings, out var language))
        {
            PageLanguage.Set(context, language);
            context.Request.Path = "/";
        }

        return next(context);
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = response =>
        {
            // The CLI names an asset by its content (main-K3Q7X2BA.js): a change is a new name, so the old one may live
            // a year in any cache. A stable name (favicon.ico) is asked about every time; the ETag answers 304.
            response.Context.Response.Headers.CacheControl = FingerprintedName().IsMatch(response.File.Name)
                ? "public, max-age=31536000, immutable"
                : "no-cache";
        },
    });
}

// Routing picks the endpoint only from here. Left implicit it would run FIRST, and the "{*path:file}" fallback below
// would claim every asset before the static files above could serve it (a built app with every script answered 404).
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();

// /docs: the one API-docs page. Platform people only; everyone else gets 404, so nothing is advertised. The wall sits
// after who-you-are and BEFORE the deny-by-default policy, which would otherwise answer the page's files with a sign-in
// redirect; it runs before Swagger UI's own middleware (the page and its assets) and before the /docs/{service}.json door.
var apiDocs = app.Services.GetRequiredService<IOptions<ApiDocsSettings>>().Value;
if (apiDocs.Services.Length > 0 && !Guid.TryParse(app.Configuration[ApiDocsPage.PlatformTenantKey], out _))
{
    throw new InvalidOperationException($"{ApiDocsPage.PlatformTenantKey} must be set when {ApiDocsSettings.Section} lists services: the /docs wall needs to know the platform tenant.");
}
app.UseWhen(context => context.Request.Path.StartsWithSegments("/docs"), docs =>
{
    docs.Use(async (context, next) =>
    {
        if (apiDocs.Services.Length > 0 && await ApiDocsPage.IsPlatformPersonAsync(context))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
    });
    docs.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "docs";
        options.DocumentTitle = "Harmony API docs";
        foreach (var service in apiDocs.Services)
        {
            options.SwaggerEndpoint($"/docs/{service.Name.ToLowerInvariant()}.json", service.Name);
        }
    });
});

app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "alive" })).AllowAnonymous();

// The W3C "Passkey Endpoints Well-Known URL": a password manager asks the relying-party domain where a person adds or
// manages passkeys and sends them there. Public, a plain JSON document with the two links, cacheable for a day, never a
// redirect (the spec forbids one); HEAD answers like GET. The front door routes the apex path here.
if (bffSettings.ServesPasskeyEndpoints)
{
    app.MapMethods("/.well-known/passkey-endpoints", [HttpMethods.Get, HttpMethods.Head], (HttpContext http) =>
    {
        http.Response.Headers.CacheControl = "public, max-age=86400";
        return Results.Json(new { enroll = bffSettings.PasskeyEnrollUrl, manage = bffSettings.PasskeyManageUrl });
    }).AllowAnonymous();
}

// The browser never signs in itself: this door starts the dance and the BFF does the rest (RFC 10017 §6.1).
// Only a LOCAL path may ride along, or a crafted link could land a fresh sign-in on someone else's site.
app.MapGet("/bff/login", (string? returnUrl) =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = LocalPathOnly(returnUrl) },
        [OpenIdConnectDefaults.AuthenticationScheme])).AllowAnonymous();

// Who am I: the few claims the web app needs to draw itself. Never cached, never a token. The endpoint answers
// anonymity ITSELF with 401 — a generic challenge here would start an OIDC redirect no fetch() can follow.
app.MapGet("/bff/user", (ClaimsPrincipal user, HttpContext http) =>
{
    http.Response.Headers.CacheControl = "no-store";
    if (user.Identity?.IsAuthenticated != true)
    {
        return Results.Unauthorized();
    }

    return Results.Json(new
    {
        sub = user.FindFirstValue("sub"),
        name = user.FindFirstValue("name"),
        tenant = user.FindFirstValue("tenant_id"),
        roles = user.FindAll("role").Select(claim => claim.Value).ToArray(),
        // How the person signed in (pwd, and mfa when the authenticator was used), from the id_token.
        amr = user.FindAll("amr").Select(claim => claim.Value).ToArray(),
        // The sign-out link carries this session's own sid: the proof a cross-site page cannot know.
        logoutUrl = "/bff/logout?sid=" + Uri.EscapeDataString(user.FindFirstValue("sid") ?? string.Empty),
    });
}).AllowAnonymous();

// Where the browser sends CSP and Trusted Types violations. See CspViolationReports for what keeps it harmless.
app.MapPost(CspViolationReports.Path, CspViolationReports.ReceiveAsync).AllowAnonymous().RequireRateLimiting(CspViolationReports.RateLimitPolicy);

// Signing out is a top-level NAVIGATION, never a fetch(): Identity ends its own session only when the browser
// itself walks to its end-session door carrying Identity's cookie, which a redirect inside a fetch() never does.
// A navigation cannot carry the CSRF header, so the proof here is the session's own sid in the link — a value no
// cross-site page knows (Duende BFF's own scheme) — on top of the Strict cookie no cross-site request carries.
// The refresh token is revoked FIRST — after this call, nothing the BFF ever held can mint another token — then
// the local session dies, then Identity's.
app.MapGet("/bff/logout", async (string? sid, HttpContext http, IHttpClientFactory clients, ClientAssertionFactory assertions, IOptions<BffSettings> bff, IOptionsMonitor<OpenIdConnectOptions> oidc, ILogger<Program> logger) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
    {
        // Nothing to end: home, where the app asks who you are and sends you to sign in.
        return Results.Redirect("/");
    }

    if (string.IsNullOrEmpty(sid) || !string.Equals(sid, http.User.FindFirstValue("sid"), StringComparison.Ordinal))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var refreshToken = await http.GetTokenAsync("refresh_token");
    if (!string.IsNullOrEmpty(refreshToken))
    {
        try
        {
            var configuration = await oidc.Get(OpenIdConnectDefaults.AuthenticationScheme).ConfigurationManager!.GetConfigurationAsync(http.RequestAborted);
            var revocationEndpoint = configuration.AdditionalData.TryGetValue("revocation_endpoint", out var found) && found is string endpoint
                ? endpoint
                : bff.Value.Authority.TrimEnd('/') + "/connect/revocation";
            using var answer = await clients.CreateClient(CookieOidcRefresher.BackchannelClientName).PostAsync(
                revocationEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = refreshToken,
                    ["token_type_hint"] = "refresh_token",
                    ["client_id"] = bff.Value.ClientId,
                    ["client_assertion_type"] = ClientAssertionFactory.AssertionType,
                    ["client_assertion"] = assertions.Create(bff.Value.Authority),
                }),
                http.RequestAborted);
            if (!answer.IsSuccessStatusCode)
            {
                logger.LogWarning("Revoking the refresh token answered {Status}; the sign-out continues.", (int)answer.StatusCode);
            }
        }
        catch (Exception exception)
        {
            // A revocation hiccup never traps a person in a session they asked to leave.
            logger.LogWarning(exception, "Revoking the refresh token failed; the sign-out continues.");
        }
    }

    // At sign-out the browser itself wipes what the person left on this site (cached pages, every cookie, local
    // storage), so a shared front-desk PC hands the next shift nothing. "cookies" reaches the whole registrable domain,
    // Identity's session cookie included: at sign-out, that is the point. The theme and language hints go too.
    http.Response.Headers["Clear-Site-Data"] = "\"cache\", \"cookies\", \"storage\"";
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties { RedirectUri = "/" });
    return Results.Empty;
}).AllowAnonymous();

// Identity's server-to-server kill switch: no cookie, no browser — the signed logout_token is the whole proof.
app.MapPost(BackChannelLogout.Path, BackChannelLogout.HandleAsync).AllowAnonymous();

// Mapped last: every named /bff door above answers first; everything /api flows through the allow-list.
// The wall above decides for /docs: this door says anonymous so the fallback policy does not answer with a sign-in
// redirect first (404 for everyone the wall refuses, never a hint).
app.MapGet("/docs/{service}.json", ApiDocsPage.GetDocumentAsync).AllowAnonymous();

// Inside-only paths that every service happens to serve under /api/{service}/: the public front door never forwards
// them, whatever the method. Developers read them on the /docs page or on the service itself, inside the network.
app.Map("/api/{service}/swagger/{**rest}", () => Results.NotFound()).AllowAnonymous();
app.Map("/api/{service}/healthchecks/{**rest}", () => Results.NotFound()).AllowAnonymous();

app.MapReverseProxy();

if (bffSettings.ServeStaticFiles)
{
    // Every path that is no door above is the web app's own (its router reads the address): the page, rendered
    // with this request's nonce.
    app.MapFallback(WebAppPage.RenderAsync).AllowAnonymous();
}
else
{
    // Development, where the dev server serves the app: any other path is nobody's — a plain 404, said out
    // loud. Without it the deny-by-default fallback policy CHALLENGES the stranger: a full PAR push and a
    // sign-in redirect for /favicon.ico.
    app.MapFallback(() => Results.NotFound()).AllowAnonymous();
}

// Both fallbacks above skip file-like paths (/old-bundle.js, /nothing.css): with no endpoint, the deny-by-default
// policy would challenge the stranger (a PAR push at Identity and a sign-in redirect for every such hit). A plain 404.
app.MapFallback("{*path:file}", () => Results.NotFound()).AllowAnonymous();

await app.RunAsync();

// The raw Redis connection Data Protection persists its key ring over, from the same settings the cache uses.
static ConfigurationOptions RedisConnection(IConfiguration configuration)
{
    var redis = configuration.GetSection("CacheSettings:CacheProviders:Redis");
    var options = new ConfigurationOptions
    {
        User = redis["User"],
        Password = redis["Password"],
        Ssl = bool.TryParse(redis["Ssl"], out var ssl) && ssl,
    };
    options.EndPoints.Add($"{redis["Connection"]}:{redis["Port"]}");
    return options;
}

// A path like "/reservations" is welcome; "//evil.example", "https://evil.example" and friends fall back to home.
static string LocalPathOnly(string? returnUrl)
    => !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) && !returnUrl.Contains(':')
        ? returnUrl
        : "/";

static void ConfigureAuthentication(WebApplicationBuilder builder)
{
    builder.Services
        .AddAuthentication(options =>
        {
            // The cookie IS the session; Identity is only ever consulted through the OIDC dance.
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            var settings = builder.Configuration.GetSection(BffSettings.Section).Get<BffSettings>() ?? new BffSettings();

            // RFC 10017 §6.1.3.2: HttpOnly and Secure are MUSTs, SameSite=Strict, Path=/ and the __Host- prefix
            // are its SHOULDs — all of them taken. The browser itself enforces the prefix's promises.
            options.Cookie.Name = settings.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(settings.SessionIdleMinutes);
            options.SlidingExpiration = true;

            // The callers are fetch() calls, not page loads: an expired session must answer 401/403,
            // never a redirect the script cannot follow.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        })
        .AddOpenIdConnect(options =>
        {
            var settings = builder.Configuration.GetSection(BffSettings.Section).Get<BffSettings>() ?? new BffSettings();

            options.Authority = settings.Authority;
            options.ClientId = settings.ClientId;
            options.ResponseType = OpenIdConnectResponseType.Code;

            options.Scope.Clear();
            foreach (var scope in settings.Scopes)
            {
                options.Scope.Add(scope);
            }

            // Our server advertises PAR, so anything less refuses to run (fail closed); PKCE is on by default.
            options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Require;
            options.RequireHttpsMetadata = true;

            // Tokens ride inside the session ticket, and the ticket lives in Redis.
            options.SaveTokens = true;

            // Who the person is comes from userinfo (the id_token stays lean): the name arrives by default,
            // role ids and the tenant through the harmony.profile scope — each array element its own claim.
            options.GetClaimsFromUserInfoEndpoint = true;
            options.ClaimActions.MapJsonKey("role", "role");
            options.ClaimActions.MapJsonKey("tenant_id", "tenant_id");

            // Raw claim names (sub, name, role, sid) — no legacy SOAP renaming; sid is the back-channel logout key.
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = "role";

            // Front-channel logout (the handler's built-in /signout-oidc receiver, sid+iss checked against THIS
            // session) is BEST-EFFORT ONLY: it runs in an iframe on Identity's logout page, and browsers that
            // block third-party cookies may never send our session cookie there. The reliable kill is the
            // back-channel receiver (2.6) and the refresh refusal — never lean on front-channel alone.

            // The session's birth moment, for the absolute cap: however lively, no cookie outlives its shift.
            options.Events.OnTokenValidated = context =>
            {
                context.Properties!.SetString(CookieOidcRefresher.SessionStartedKey, DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
                return Task.CompletedTask;
            };

            // The handshake's own short-lived cookies, pinned explicitly: always Secure, and SameSite=None on
            // purpose — the form_post answer arrives as a cross-site POST and must still see them.
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
            options.CorrelationCookie.HttpOnly = true;
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
            options.NonceCookie.HttpOnly = true;

            // A failed or cancelled sign-in (wrong state, lost correlation, Cancel at Identity) is an everyday
            // event, never a stack trace: log the reason, send the person home.
            options.Events.OnRemoteFailure = context =>
            {
                context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>()
                    .LogWarning("Sign-in could not finish: {Reason}", context.Failure?.Message);
                context.Response.Redirect("/");
                context.HandleResponse();
                return Task.CompletedTask;
            };

            // private_key_jwt at BOTH moments client authentication happens: pushing the authorization
            // request (PAR) and redeeming the code. No client secret exists to configure.
            options.Events.OnPushAuthorization = context =>
            {
                var assertions = context.HttpContext.RequestServices.GetRequiredService<ClientAssertionFactory>();
                context.ProtocolMessage.ClientAssertionType = ClientAssertionFactory.AssertionType;
                context.ProtocolMessage.ClientAssertion = assertions.Create(settings.Authority);
                return Task.CompletedTask;
            };
            options.Events.OnAuthorizationCodeReceived = context =>
            {
                var assertions = context.HttpContext.RequestServices.GetRequiredService<ClientAssertionFactory>();
                context.TokenEndpointRequest!.ClientAssertionType = ClientAssertionFactory.AssertionType;
                context.TokenEndpointRequest.ClientAssertion = assertions.Create(settings.Authority);
                return Task.CompletedTask;
            };

            if (builder.Environment.IsDevelopment())
            {
                // Development only: the local dev certificate is not in every store this host validates against.
                // Production keeps full certificate validation — this branch cannot be reached outside Development.
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                };
            }
        });
}

/// <summary>What the top-level statements need as members: the asset-name pattern the static file caching reads.</summary>
public partial class Program
{
    /// <summary>A content-hashed asset name the CLI emits: main-K3Q7X2BA.js, styles-9F2C1D0E.css, media.K3Q7X2BA.woff2.</summary>
    [GeneratedRegex(@"[-.][0-9A-Za-z]{8}\.(js|mjs|css|woff2?|ttf|otf|png|jpe?g|gif|svg|webp|ico|json|map)$")]
    private static partial Regex FingerprintedName();
}
