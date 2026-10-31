using Duende.IdentityServer;
using Harmony.Core.DI;
using Harmony.Core.Enums;
using Harmony.Core.EventBus.Abstractions;
using Harmony.Core.Identity.DI;
using Harmony.Core.Identity.Permissions;
using Harmony.Core.Localization.DI;
using Harmony.Core.Logging.DI;
using Harmony.Core.Models;
using Harmony.Core.Notification.DI;
using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Caching;
using Harmony.Identity.Infrastructure.CompanyLogin;
using Harmony.Identity.Infrastructure.BackgroundServices.DataRetention;
using Harmony.Identity.Infrastructure.IdentityServer;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Harmony.Identity.Infrastructure.IdentityServer.Yarp;
using Harmony.Identity.Infrastructure.Mail;
using Harmony.Identity.Infrastructure.Messaging;
using Harmony.Identity.Infrastructure.Passkeys;
using Harmony.Identity.Infrastructure.Tenants;
using Harmony.Identity.Infrastructure.Passwords;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Identity.Infrastructure.Proxies;
using Harmony.Identity.Infrastructure.RateLimiting;
using Harmony.Identity.Infrastructure.Repositories;
using Harmony.Identity.Infrastructure.Services;
using Harmony.Identity.Infrastructure.Signing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Reflection;

namespace Harmony.Identity.Infrastructure.DI;


public static class DependenciesConfigurator
{
    public static void AddInfrastructureService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuildingBlocks(configuration);
        services.AddCore(configuration);
        services.AddFrameworkServices(configuration);
    }

    public static void ConfigureAppLogging(this IApplicationBuilder app)
    {
        app.UseCore(coreBuilderConfig =>
        {
            coreBuilderConfig.UseExceptionHandling();
            coreBuilderConfig.UseSwagger();
            coreBuilderConfig.UseHealthChecks();
        });

        // Duende's endpoints (discovery, jwks, token) are the one anonymous door; everything after sits behind the platform token.
        app.UseIdentityServer();

        app.UseIdentity(identityBuilderConfig =>
        {
            identityBuilderConfig.UsePlatformAuthentication();
        });
    }

    public static void ConfigureInfrastructureSettings(this IConfigurationBuilder configurationBuilder, IHostEnvironment hostingEnvironment)
    {
        configurationBuilder.ConfigureLoggingConfiguration(hostingEnvironment);
        configurationBuilder.ConfigureLocalizationConfiguration(hostingEnvironment);
        configurationBuilder.ConfigureNotificationConfiguration(hostingEnvironment);
        configurationBuilder.ConfigureIdentityConfiguration(hostingEnvironment);
    }

    private static void AddBuildingBlocks(this IServiceCollection services, IConfiguration configuration)
    {
        Harmony.Core.BuildingBlocks.DI.DependenciesConfigurator.AddBuildingBlocks(services, configuration, config =>
        {
            config.AddDatabaseContext<IdentityDbContext>(ServiceLifetime.Scoped);
            config.AddReadOnlyRepository<LookupCategory, IdentityDbContext>();
            config.AddReadOnlyRepository<LookupValue, IdentityDbContext>();
            config.AddReadOnlyRepository<Role, IdentityDbContext>();
            config.AddReadOnlyRepository<User, IdentityDbContext>();
            config.AddRepositoriesFromAssemblyContaining<RoleRepository>();
            config.AddMediatR(mediatRConfig =>
            {
                mediatRConfig.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
                mediatRConfig.AddValidationBehavior();

                // Events added during a command are published when it is done (see Messaging).
                if (IdentityMessaging.IsOn(configuration))
                {
                    mediatRConfig.AddEventBusPublishingBehavior();
                }
            });
        });

        // The framework's system-events handler needs the bus: without messaging it goes.
        if (!IdentityMessaging.IsOn(configuration))
        {
            services.RemoveAll<IIntegrationEventHandler<SystemEvent>>();
        }
    }

    private static void AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        var tenantId = configuration["ApplicationSettings:ApplicationKeys:PlatformTenantId"]!;

        Harmony.Core.DI.DependenciesConfigurator.AddCore(services, configuration, coreServicesConfig =>
        {
            coreServicesConfig.AddTransactionService();
            coreServicesConfig.AddSensitiveDataResolver();
            coreServicesConfig.AddSensitiveDataSerialization();

            // The tenant comes from the token now, not from a header: Swagger gets an Authorize button instead.
            coreServicesConfig.AddSwagger(conf =>
            {
                conf.EnableJWTAuthorization();

                var infrastructureXmlPath = Path.Combine(System.AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
                if (File.Exists(infrastructureXmlPath))
                {
                    conf.IncludeXmlComments(infrastructureXmlPath);
                }

                var apiXmlPath = Path.Combine(System.AppContext.BaseDirectory, "Harmony.Identity.Api.xml");
                if (File.Exists(apiXmlPath))
                {
                    conf.IncludeXmlComments(apiXmlPath);
                }
            });

            coreServicesConfig.AddHealthChecks(healthChecksConfig =>
            {
                healthChecksConfig.AddHealthCheck("Database", eHealthChecksType.SqlServer);
            });

            coreServicesConfig.AddAppInfo(appInfoConfig =>
            {
                appInfoConfig.Name = "Identity";
                appInfoConfig.Title = "Harmony.Identity";
                appInfoConfig.Description = "Harmony Identity API";
                appInfoConfig.ApiTitle = "Harmony Identity API";
                appInfoConfig.ApiDescription = "Harmony Identity API HTTP";
            });

            coreServicesConfig.AddMultiTenancy((serviceProvider, multiTenancyConfig) =>
            {
                var dbEngine = Enum.Parse<eDbEngine>(configuration["ApplicationSettings:ApplicationKeys:DefaultDbEngine"]!);
                var connectionString = configuration["ApplicationSettings:ConnectionStrings:Default"]!;

                // One database for every tenant (the design's one-database model): the platform tenant is the one
                // listed, and every customer tenant, born in the Tenants table, runs with its settings under its own id.
                multiTenancyConfig.SharedDatabase = true;

                // The bus settings ride on the listed tenant from the start, so every customer tenant (made from it) has them.
                var bus = configuration.GetSection(IdentityMessaging.Section);
                multiTenancyConfig.AddOrUpdateTenant(
                    tenantId,
                    "Harmony",
                    "en",
                    database => { database.Configure(dbEngine, connectionString); },
                    IdentityMessaging.IsOn(configuration)
                        ? eventBus =>
                        {
                            eventBus.UseDefaultCredentials = true;
                            eventBus.Configure(bus["DefaultEndpoint"]!, bus["DefaultHost"]!, bus.GetValue<int>("DefaultPort"), bus["DefaultUserName"]!, bus["DefaultPassword"]!);
                        }
                        : null);
            });
        });
    }

    private static void AddFrameworkServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddCache(configuration);
        services.AddJwtServices(configuration);
        services.AddInfrastructureServices(configuration);

        // Mail over SMTP: the provider (a mailbox, Azure Communication Services, SES) is a settings change.
        services.AddNotification(configuration, notification => notification.AddSmtpEmailClient());

        // What Identity tells the other services: the framework's bus and outbox, or nothing when the settings say so.
        services.AddMessaging(configuration);
    }

    private static void AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        Harmony.Core.Cache.DI.DependenciesConfigurator.AddCache(services, configuration, config =>
        {
            config.AddMemoryCache();
            config.AddRedisCache();
            config.AddRedisDistributedLock();
            config.AddMemoryWithRedisInvalidatorCache();
            // The receipt gate's store. A write that carries a receipt runs once; its repeats replay the answer.
            config.AddReceiptStore();
        });
    }

    /// <summary>
    /// Identity protects its own API the way every Harmony service does: the framework validates the Bearer
    /// token against this issuer's own discovery document (IdentitySettings:Platform), deny by default.
    /// </summary>
    private static void AddJwtServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Duende FIRST. Both calls register an authentication default and the last one registered wins:
        // with Duende last, its cookie scheme becomes the default and every API call is sent to a login
        // page instead of Bearer validation. Verified live on 2026-10-02.
        services.AddIdentityServerHost(configuration);

        // Stage A: sign-in with the company's Microsoft or Google account (each only when its settings are present).
        services.AddCompanyLogin(configuration);
        services.AddDataProtectionKeyRing(configuration);

        services.AddIdentity(configuration, identityConfig =>
        {
            identityConfig.AddPlatformAuthentication();
        });
    }

    private static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Scoped like the Redis cache and the DbContext it wraps.
        services.TryAddScoped<ICacheService, CacheService>();
        services.TryAddScoped<ICacheSeeder, CacheSeeder>();
        services.TryAddScoped<ISubscriptionPublisher, SubscriptionPublisher>();
        services.TryAddScoped<ISessionService, SessionService>();
        services.TryAddScoped<MfaChallengeStore>();

        // The sign-in rules behind the sign-in pages (the only way in since BFF step 5), in one place each.
        services.TryAddScoped<PasswordCheck>();
        services.TryAddScoped<SecondFactorCheck>();
        services.TryAddScoped<TwoStepSetup>();
        services.TryAddScoped<EmailSignIn>();
        services.TryAddScoped<NewDeviceApproval>();
        services.TryAddScoped<ForcedPasswordChange>();

        // Item 6: passkeys. The sign-in half on the pages, the registration half behind the API (the challenge held server-side).
        services.TryAddScoped<PasskeySignIn>();
        services.TryAddScoped<PasskeyRegistrationHold>();
        services.TryAddScoped<IdentityConfirmation>();
        // The platform's own tenant: the id every service knows, and the names its seeded row carries.
        services.AddOptions<PlatformTenantSettings>()
            .Bind(configuration.GetSection(PlatformTenantSettings.Section))
            .PostConfigure(settings => settings.TenantId = Guid.TryParse(configuration["ApplicationSettings:ApplicationKeys:PlatformTenantId"], out var id) ? id : Guid.Empty)
            .Validate(PlatformTenantSettings.IsValid, $"{PlatformTenantSettings.Section}: Code (2-32), LegalNameEn and LegalNameAr (1-128) are required, and ApplicationSettings:ApplicationKeys:PlatformTenantId must be a GUID.")
            .ValidateOnStart();

        services.AddOptions<PasskeySettings>()
            .Bind(configuration.GetSection(PasskeySettings.Section))
            .Validate<IOptions<WebBffSettings>, IOptions<ConsoleBffSettings>, IOptions<TokenIssuerSettings>>(
                (settings, bff, console, issuer) => PasskeySettings.IsValid(settings, bff.Value.Origins.Concat(console.Value.Origins), issuer.Value.Issuer),
                $"{PasskeySettings.Section}:ServerDomain must be a domain the issuer's host and every WebBff and ConsoleBff origin's host equal or sit under (empty = the issuer's host, right only when they are all that host).")
            .ValidateOnStart();
        services.ConfigureOptions<PasskeyOptionsSetup>();

        // Item 4: builds security-trail rows from the request (the handlers save them through their repositories).
        services.TryAddScoped<ISecurityEventFactory, SecurityEventFactory>();

        // The BFF client's settings, checked at startup: a non-https origin, or a missing / wrong / private key, stops the service.
        services.AddOptions<WebBffSettings>()
            .Bind(configuration.GetSection(WebBffSettings.Section))
            .Validate(settings => settings.Origins.Length > 0 && settings.Origins.All(WebLinkSettings.IsValidOrigin), $"{WebBffSettings.Section}:Origins must list at least one https origin without a path.")
            .Validate(settings => WebBffSettings.ReadPublicJwk(settings.ClientPublicKeyPath) is not null, $"{WebBffSettings.Section}:ClientPublicKeyPath must name a PEM file holding an EC P-256 public key, and no private key.")
            .Validate(settings => settings.PreviousClientPublicKeyPaths.All(path => WebBffSettings.ReadPublicJwk(path) is not null), $"{WebBffSettings.Section}:PreviousClientPublicKeyPaths must each name a PEM file holding an EC P-256 public key, and no private key.")
            .ValidateOnStart();

        // The console BFF client's settings (B9): no section, no console client; a present one obeys the web client's rules.
        services.AddOptions<ConsoleBffSettings>()
            .Bind(configuration.GetSection(ConsoleBffSettings.Section))
            .Validate(settings => !settings.IsPresent || settings.Origins.All(WebLinkSettings.IsValidOrigin), $"{ConsoleBffSettings.Section}:Origins must list https origins without a path.")
            .Validate(settings => !settings.IsPresent || BffClientSettings.ReadPublicJwk(settings.ClientPublicKeyPath) is not null, $"{ConsoleBffSettings.Section}:ClientPublicKeyPath must name a PEM file holding an EC P-256 public key, and no private key.")
            .Validate(settings => !settings.IsPresent || settings.PreviousClientPublicKeyPaths.All(path => BffClientSettings.ReadPublicJwk(path) is not null), $"{ConsoleBffSettings.Section}:PreviousClientPublicKeyPaths must each name a PEM file holding an EC P-256 public key, and no private key.")
            .ValidateOnStart();

        // The console's sign-in frame: optional words and links; empty values leave the line out.
        services.AddOptions<SignInPagesSettings>()
            .Bind(configuration.GetSection(SignInPagesSettings.Section));

        services.TryAddScoped<IAccountMailer, AccountMailer>();
        services.AddOptions<WebLinkSettings>()
            .Bind(configuration.GetSection(WebLinkSettings.Section))
            .Validate(settings => WebLinkSettings.IsValidTemplate(settings.ResetPasswordUrl), $"{WebLinkSettings.Section}:ResetPasswordUrl must be an absolute https URL containing {{email}} and {{token}}.")
            .Validate(settings => WebLinkSettings.IsValidTemplate(settings.InvitationUrl), $"{WebLinkSettings.Section}:InvitationUrl must be an absolute https URL containing {{email}} and {{token}}.")
            .ValidateOnStart();

        // The leaked-password check (Have I Been Pwned); the section is optional, its defaults are the public service.
        services.AddOptions<LeakedPasswordSettings>()
            .Bind(configuration.GetSection(LeakedPasswordSettings.Section))
            .Validate(LeakedPasswordSettings.IsValid, $"{LeakedPasswordSettings.Section}:RangeUrl must be an absolute https URL ending in '/' (http only on localhost), and TimeoutSeconds 1 to 30.")
            .ValidateOnStart();
        services.AddHttpClient(LeakedPasswordValidator.ClientName, client => client.DefaultRequestHeaders.UserAgent.ParseAdd("Harmony-Identity"));

        // Item 5: tries per caller address at the anonymous doors; the section is optional, its defaults fit a hotel.
        services.AddOptions<RateLimitSettings>()
            .Bind(configuration.GetSection(RateLimitSettings.Section))
            .Validate(RateLimitSettings.IsValid, $"{RateLimitSettings.Section}: every limit must be 1 or more, and WindowMinutes 1 to 60.")
            .ValidateOnStart();

        // G1: the data retention periods and the nightly background cleanup that enforces them - the same
        // hosted-job shape as Duende's token cleanup below. The section is optional: the defaults are the legal floor.
        services.AddOptions<DataRetentionSettings>()
            .Bind(configuration.GetSection(DataRetentionSettings.Section))
            .Validate(DataRetentionSettings.IsValid, $"{DataRetentionSettings.Section}: SecurityTrailMonths 12 to 120, UnseenDeviceDays 30 to 400, CleanupHourUtc 0 to 23, StartDelaySeconds 5 to 3600, DeleteBatchSize 100 to 50000.")
            .ValidateOnStart();
        services.TryAddScoped<IDataRetentionCleanupService, DataRetentionCleanupService>();
        services.AddHostedService<DataRetentionBackgroundService>();
        // Stage B: records of sessions that expired without a sign-out are ended every few minutes.
        services.AddHostedService<Harmony.Identity.Infrastructure.BackgroundServices.SessionRecords.SessionRecordClosingService>();
        // Stage C: the revocation feed is read through its own repository and compacted every hour.
        services.TryAddScoped<Harmony.Identity.Domain.Repositories.IRevocationFeedRepository, RevocationFeedRepository>();
        services.AddHostedService<Harmony.Identity.Infrastructure.BackgroundServices.RevocationFeed.RevocationCompactionService>();
        // Stage D: risky changes wait for a second administrator when the customer requires approvals.
        services.TryAddScoped<IApprovalGate, Harmony.Identity.Infrastructure.Approvals.ApprovalGate>();
        // Stage E: the company directory's door (SCIM 2.0) and the nightly inactivity rule.
        services.TryAddScoped<IDirectoryCredentials, Harmony.Identity.Infrastructure.Provisioning.DirectoryCredentials>();
        services.TryAddScoped<IInactivitySuspension, Harmony.Identity.Infrastructure.BackgroundServices.Inactivity.InactivitySuspension>();
        services.AddHostedService<Harmony.Identity.Infrastructure.BackgroundServices.Inactivity.InactivitySuspensionService>();
        // Stage F: the inbox a receiver of other services' events writes in its own save.
        services.TryAddScoped<Harmony.Identity.Domain.Repositories.IInboxRepository, InboxRepository>();

        // Item 5: the real caller address (X-Forwarded-For), believed only from the front doors the settings list.
        services.AddOptions<TrustedProxySettings>()
            .Bind(configuration.GetSection(TrustedProxySettings.Section))
            .Validate(TrustedProxySettings.IsValid, $"{TrustedProxySettings.Section}: every entry must be an IP address (Addresses) or a range like 172.17.0.0/16 (Networks).")
            .ValidateOnStart();
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<TrustedProxySettings>>((options, settings) =>
            {
                var proxies = settings.Value;

                // Microsoft trusts this machine by default; we trust only the list. Careful: with BOTH lists empty Microsoft
                // trusts EVERY sender, so no proxy listed means the header is not read at all.
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                if (proxies.Addresses.Length == 0 && proxies.Networks.Length == 0)
                {
                    options.ForwardedHeaders = ForwardedHeaders.None;
                    return;
                }

                // The caller's address AND scheme: behind a front door that ends TLS, Duende would otherwise print http:// URLs.
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                // Walk back through every listed proxy to the first address not listed: that is the caller. Microsoft's default
                // stops after one hop, so behind the front door AND the BFF everyone would count as the front door. Safe only
                // because the lists are never empty here (Microsoft: no limit only with known proxies or networks).
                options.ForwardLimit = null;
                foreach (var address in proxies.Addresses)
                {
                    options.KnownProxies.Add(IPAddress.Parse(address));
                }

                foreach (var network in proxies.Networks)
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
            });

        // Item 5: the limits per caller at the sign-in, forgot and reset doors (switched on per door with [EnableRateLimiting]).
        services.AddSignInRateLimits();

        // No CORS on purpose (BFF step 5): no browser calls this service's API or token endpoint directly any more. The
        // web app talks to its BFF on its own origin, and the BFF talks to this service server to server.

        // Identity answers a cache miss from its own database and rewrites the key. The framework readers in the
        // other services cannot, so Identity also republishes everything the first time it finds Redis wiped.
        services.Replace(ServiceDescriptor.Scoped<IPlatformPermissionResolver, DatabaseBackedPermissionResolver>());
        services.Replace(ServiceDescriptor.Scoped<ISecurityVersionResolver, DatabaseBackedSecurityVersionResolver>());
        services.AddUserAccounts();
        services.AddTokenSigning(configuration);
    }

    /// <summary>
    /// ASP.NET Core Identity for users, stored in OUR DbContext: UserOnlyStore needs only a DbContext
    /// that maps Identity's four tables, so the framework's tenant filter, audit and rowversion apply
    /// to users too. No Identity roles: Harmony's Role aggregate carries the rules Identity's roles lack.
    /// </summary>
    private static void AddUserAccounts(this IServiceCollection services)
    {
        // Fully qualified: Identity's package also has a type named IdentityDbContext.
        // Five wrong passwords in a row lock the account for five minutes. Passwords: 12+ characters with a symbol.
        services.AddIdentityCore<User>(options =>
        {
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Password.RequiredLength = PasswordRules.MinimumLength;
            // Like Mews: at least one symbol (anything but A-Z, a-z, 0-9; a space counts). No other mix of character kinds.
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = false;
        })
            .AddUserStore<HarmonyUserStore>()
            // NIST SP 800-63B-4 blocklist, after Identity's own rules: no service name or own name, no leaked password.
            .AddPasswordValidator<ContextWordPasswordValidator>()
            .AddPasswordValidator<LeakedPasswordValidator>()
            // RFC 6238 authenticator codes (Google / Microsoft Authenticator). The only token provider we need.
            .AddTokenProvider<AuthenticatorTokenProvider<User>>(TokenOptions.DefaultAuthenticatorProvider)
            // One-time links (password reset, invitation): Data Protection tokens bound to the security stamp, 24 h.
            .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenOptions.DefaultProvider);

        // Identity's WebAuthn handler (AddIdentityCore registers the store side only; AddIdentity would register this too).
        services.TryAddScoped<IPasskeyHandler<User>, PasskeyHandler<User>>();
    }

    /// <summary>
    /// The signing keys, checked at start: a bad issuer or key file stops the service before it signs anything.
    /// </summary>
    private static void AddTokenSigning(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TokenIssuerSettings>()
            .Bind(configuration.GetSection(TokenIssuerSettings.Section))
            .Validate(
                settings => Uri.TryCreate(settings.Issuer, UriKind.Absolute, out var issuer) && issuer.Scheme == Uri.UriSchemeHttps,
                $"{TokenIssuerSettings.Section}:Issuer must be an absolute https URL.")
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.SigningKeyPem) || !string.IsNullOrWhiteSpace(settings.SigningKeyPath),
                $"{TokenIssuerSettings.Section}:SigningKeyPem or :SigningKeyPath is required.")
            .Validate(
                settings => string.IsNullOrWhiteSpace(settings.SigningKeyPath) || File.Exists(settings.SigningKeyPath),
                $"{TokenIssuerSettings.Section}:SigningKeyPath does not exist.")
            .Validate(
                settings => ConfiguredKeys(settings).All(pem => !RsaSigningKeyProvider.TryDescribeProblem(pem, out _)),
                $"{TokenIssuerSettings.Section}: a signing key is not a readable RSA private key of at least {RsaSigningKeyProvider.MinimumKeyBits} bits.")
            .ValidateOnStart();

        services.AddSingleton<RsaSigningKeyProvider>();
    }

    private static IEnumerable<string> ConfiguredKeys(TokenIssuerSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.SigningKeyPem))
        {
            yield return settings.SigningKeyPem;
        }
        else if (!string.IsNullOrWhiteSpace(settings.SigningKeyPath) && File.Exists(settings.SigningKeyPath))
        {
            yield return File.ReadAllText(settings.SigningKeyPath);
        }

        foreach (var pem in settings.PreviousSigningKeyPems.Where(pem => !string.IsNullOrWhiteSpace(pem)))
        {
            yield return pem;
        }
    }

    /// <summary>
    /// Duende IdentityServer as the token engine. It signs with our key ring (S3a); automatic key
    /// management is off. The licence key is read from configuration (Duende:IdentityServer:LicenseKey).
    /// Sign-in flows and clients arrive with S5. 🔴 SERVICES: the harmony:service grant lived here.
    /// </summary>
    /// <summary>A BFF host's public keys as JSON Web Keys: the current one first, then those kept during a rotation.</summary>
    private static string[] PublicJwksOf(BffClientSettings host)
        => new[] { host.ClientPublicKeyPath }.Concat(host.PreviousClientPublicKeyPaths)
            .Select(BffClientSettings.ReadPublicJwk)
            .OfType<string>()
            .ToArray();

    private static void AddIdentityServerHost(this IServiceCollection services, IConfiguration configuration)
    {
        var bff = configuration.GetSection(WebBffSettings.Section).Get<WebBffSettings>() ?? new WebBffSettings();
        var bffPublicKeys = PublicJwksOf(bff);

        // The console host (B9): registered only when its section names an origin.
        var console = configuration.GetSection(ConsoleBffSettings.Section).Get<ConsoleBffSettings>() ?? new ConsoleBffSettings();
        var consolePublicKeys = console.IsPresent ? PublicJwksOf(console) : [];

        services.AddIdentityServer(options =>
        {
            options.IssuerUri = configuration[$"{TokenIssuerSettings.Section}:{nameof(TokenIssuerSettings.Issuer)}"];
            options.KeyManagement.Enabled = false;
            options.EmitStaticAudienceClaim = false;

            // private_key_jwt: a client's signed proof must name exactly this server, so a proof made for another cannot be replayed here.
            options.StrictClientAssertionAudienceValidation = true;

            // The sign-in pages (Razor, change 1.4). Duende sends the browser to these addresses.
            options.UserInteraction.LoginUrl = "/account/login";
            options.UserInteraction.LogoutUrl = "/account/logout";
            options.UserInteraction.ErrorUrl = "/account/error";

            // The APIs here use the platform token (Bearer, the default scheme). The sign-in session is Duende's own cookie, named, never guessed.
            options.Authentication.CookieAuthenticationScheme = IdentityServerConstants.DefaultCookieAuthenticationScheme;

            // The session rule: 60 minutes without activity ends the session (the same as the refresh token).
            options.Authentication.CookieLifetime = IdentityServerResources.RefreshIdleLifetime;
            options.Authentication.CookieSlidingExpiration = true;

            // The BFF's refreshes are the sign of a person still at work: each one extends the server-side session, so a
            // busy person's session (their device row, their sign-in record) is not ended at minute 60. When it does
            // expire, idle, the BFF is told at once through back-channel logout (Duende: "Inactivity timeout").
            options.Authentication.CoordinateClientLifetimesWithUserSession = true;
            options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout = true;

            // Lax: the cookie goes with a normal sign-in redirect, never with a POST, image or frame from another site.
            // Duende's default (None) exists only for the check-session iframe; the BFF does not use it, so it is off.
            options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
            options.Endpoints.EnableCheckSessionEndpoint = false;
        })
        .AddInMemoryIdentityResources(IdentityServerResources.IdentityResources)
        .AddInMemoryApiScopes(IdentityServerResources.ApiScopes)
        .AddInMemoryApiResources(IdentityServerResources.ApiResources)
        .AddInMemoryClients(IdentityServerResources.Clients(bff.Origins, bffPublicKeys, console.Origins, consolePublicKeys))
        // private_key_jwt client authentication (RFC 7523), used by the BFF.
        .AddJwtBearerClientAuthentication()
        // The token claims (tenant, roles, security version), run by Duende for every token it issues.
        .AddProfileService<HarmonyProfileService>()
        // S5 step 4: refresh tokens live in SQL (Duende operational store, same database as ours), so they survive a restart and
        // a second instance sees them. Duende cleans expired and consumed rows itself (TokenCleanupHost, hourly).
        .AddOperationalStore(store =>
        {
            store.ConfigureDbContext = db => UseIdentityDatabase(db, configuration);
            store.EnableTokenCleanup = true;
            store.RemoveConsumedTokens = true;
        })
        // Item 4: each sign-in session is kept in SQL (the ServerSideSessions table of the same store), so one session can be
        // listed and ended on its own: "sign out this device" like Mews. The cookie then carries only a reference.
        .AddServerSideSessions();

        // The sign-in cookie. __Host- means HTTPS only, this host only, the whole site; HttpOnly means no script can read it.
        services.PostConfigure<CookieAuthenticationOptions>(IdentityServerConstants.DefaultCookieAuthenticationScheme, cookie =>
        {
            cookie.Cookie.Name = "__Host-harmony-identity";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.Cookie.Path = "/";
            cookie.Cookie.Domain = null;
        });

        // Registered after AddIdentityServer so these win over Duende's automatic key stores.
        services.AddSingleton(sp => KeyRingStores.Signing(sp.GetRequiredService<RsaSigningKeyProvider>()));
        services.AddSingleton(sp => KeyRingStores.Validation(sp.GetRequiredService<RsaSigningKeyProvider>()));
    }

    /// <summary>
    /// ASP.NET Data Protection keys in SQL, next to our tables: Duende protects the stored refresh tokens with them and our user
    /// store protects the authenticator secrets. On the machine (the default) they would die with the container, and with them
    /// every stored token and every second factor. The application name pins the key ring to this service.
    /// </summary>
    private static void AddDataProtectionKeyRing(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(DataProtectionSettings.Section).Get<DataProtectionSettings>() ?? new DataProtectionSettings();
        var current = KeyRingCertificate.LoadCurrent(settings.CertificatePath);
        var previous = settings.PreviousCertificatePaths.Select(KeyRingCertificate.LoadPrevious).ToArray();

        services.AddDbContext<DataProtectionKeysDbContext>(db => UseIdentityDatabase(db, configuration));
        services.AddDataProtection()
            .SetApplicationName("Harmony.Identity")
            .PersistKeysToDbContext<DataProtectionKeysDbContext>()
            // The key ring rows are encrypted with this certificate; without its private key a database copy decrypts nothing.
            .ProtectKeysWithCertificate(current)
            .UnprotectKeysWithAnyCertificate([current, .. previous]);
    }

    /// <summary>The supporting stores (Duende grants, Data Protection keys) live in the Identity database: same engine, same connection.</summary>
    private static void UseIdentityDatabase(DbContextOptionsBuilder db, IConfiguration configuration)
    {
        var dbEngine = Enum.Parse<eDbEngine>(configuration["ApplicationSettings:ApplicationKeys:DefaultDbEngine"]!);
        var connectionString = configuration["ApplicationSettings:ConnectionStrings:Default"]!;
        var migrationsAssembly = typeof(DependenciesConfigurator).Assembly.GetName().Name;

        if (dbEngine == eDbEngine.PostgreSql)
        {
            db.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
        }
        else
        {
            db.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
        }
    }
}