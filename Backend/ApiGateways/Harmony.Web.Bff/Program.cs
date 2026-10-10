using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Harmony.Core.DI;
using Harmony.Web.Bff.Authentication;
using Harmony.Web.Bff.Sessions;
using Harmony.Web.Bff.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// The framework's one-file settings: applicationSettings(.Environment).json from the run folder, like every service.
builder.Configuration.ConfigureCoreConfiguration(builder.Environment);

// Nothing tells a caller which server software to aim at.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

builder.Services.AddOptions<BffSettings>()
    .Bind(builder.Configuration.GetSection(BffSettings.Section))
    .Validate(BffSettings.IsValid, $"{BffSettings.Section}: Authority and ApiDestination must be absolute https URLs, the private key file must exist, the cookie must keep its __Host- prefix, SessionIdleMinutes 5 to 720, SessionAbsoluteHours 1 to 24.")
    .ValidateOnStart();

// The shared fabric this host stands on: the framework core (AppInfo names every Redis key "webbff", the call
// context the lock manager leans on), the Redis cache that holds the sessions, and the distributed lock that
// makes token refresh single-flight.
Harmony.Core.DI.DependenciesConfigurator.AddCore(builder.Services, builder.Configuration, core =>
    core.AddAppInfo(app =>
    {
        app.Name = "WebBff";
        app.Title = "Harmony.Web.Bff";
        app.Description = "Harmony web BFF";
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
    .SetApplicationName("Harmony.Web.Bff")
    .PersistKeysToStackExchangeRedis(ConnectionMultiplexer.Connect(RedisConnection(builder.Configuration)), "Global:webbff:dataprotection-keys")
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

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "alive" })).AllowAnonymous();

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
        roles = user.FindAll("role").Select(claim => claim.Value).ToArray(),
    });
}).AllowAnonymous();

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

            // Tokens ride inside the session ticket; step 2.3 moves the ticket into Redis.
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = false;

            // Raw claim names (sub, name, role, sid) — no legacy SOAP renaming; sid is the back-channel logout key.
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = "role";

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
