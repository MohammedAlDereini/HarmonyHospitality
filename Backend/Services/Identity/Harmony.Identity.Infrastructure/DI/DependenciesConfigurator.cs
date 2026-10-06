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
using Harmony.Identity.Infrastructure.IdentityServer;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Harmony.Identity.Infrastructure.IdentityServer.Yarp;
using Harmony.Identity.Infrastructure.Mail;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Identity.Infrastructure.Repositories;
using Harmony.Identity.Infrastructure.Services;
using Harmony.Identity.Infrastructure.Signing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
            });
        });

        // Needs RabbitMQ + Redis; remove until the event bus is wired.
        services.RemoveAll<IIntegrationEventHandler<SystemEvent>>();
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

                multiTenancyConfig.AddOrUpdateTenant(
                    tenantId,
                    "Harmony",
                    "en",
                    database => { database.Configure(dbEngine, connectionString); });
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
    }

    private static void AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        Harmony.Core.Cache.DI.DependenciesConfigurator.AddCache(services, configuration, config =>
        {
            config.AddMemoryCache();
            config.AddRedisCache();
            config.AddRedisDistributedLock();
            config.AddMemoryWithRedisInvalidatorCache();
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
        services.TryAddScoped<ISessionRevoker, DuendeSessionRevoker>();
        services.TryAddScoped<MfaChallengeStore>();

        // The sign-in rules, shared by the sign-in pages and the password grant they replace.
        services.TryAddScoped<PasswordCheck>();
        services.TryAddScoped<SecondFactorCheck>();
        services.TryAddScoped<TwoStepSetup>();

        // The BFF client's settings, checked at startup: a non-https origin, or a missing / wrong / private key, stops the service.
        services.AddOptions<WebBffSettings>()
            .Bind(configuration.GetSection(WebBffSettings.Section))
            .Validate(settings => settings.Origins.Length > 0 && settings.Origins.All(WebLinkSettings.IsValidOrigin), $"{WebBffSettings.Section}:Origins must list at least one https origin without a path.")
            .Validate(settings => WebBffSettings.ReadPublicJwk(settings.ClientPublicKeyPath) is not null, $"{WebBffSettings.Section}:ClientPublicKeyPath must name a PEM file holding an EC P-256 public key, and no private key.")
            .Validate(settings => settings.PreviousClientPublicKeyPaths.All(path => WebBffSettings.ReadPublicJwk(path) is not null), $"{WebBffSettings.Section}:PreviousClientPublicKeyPaths must each name a PEM file holding an EC P-256 public key, and no private key.")
            .ValidateOnStart();

        services.TryAddScoped<IAccountMailer, AccountMailer>();
        services.AddOptions<WebLinkSettings>()
            .Bind(configuration.GetSection(WebLinkSettings.Section))
            .Validate(settings => WebLinkSettings.IsValidTemplate(settings.ResetPasswordUrl), $"{WebLinkSettings.Section}:ResetPasswordUrl must be an absolute https URL containing {{email}} and {{token}}.")
            .Validate(settings => WebLinkSettings.IsValidTemplate(settings.InvitationUrl), $"{WebLinkSettings.Section}:InvitationUrl must be an absolute https URL containing {{email}} and {{token}}.")
            .Validate(settings => settings.AllowedOrigins.Length > 0 && settings.AllowedOrigins.All(WebLinkSettings.IsValidOrigin), $"{WebLinkSettings.Section}:AllowedOrigins must list at least one https origin without a path.")
            .ValidateOnStart();

        // The browser side of the API: only the web app origins, with the Authorization header and the verbs we use.
        services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
            .WithOrigins(WebOrigins(configuration))
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "PUT", "DELETE")));

        // PropX: Identity answers a cache miss from its own database and rewrites the key. The framework readers in the
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
        // PropX rules: five wrong passwords in a row lock the account for five minutes; passwords are 12+ characters.
        services.AddIdentityCore<User>(options =>
        {
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Password.RequiredLength = PasswordRules.MinimumLength;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = false;
        })
            .AddUserStore<HarmonyUserStore>()
            // RFC 6238 authenticator codes (Google / Microsoft Authenticator). The only token provider we need.
            .AddTokenProvider<AuthenticatorTokenProvider<User>>(TokenOptions.DefaultAuthenticatorProvider)
            // One-time links (password reset, invitation): Data Protection tokens bound to the security stamp, 24 h.
            .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenOptions.DefaultProvider);
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
    private static void AddIdentityServerHost(this IServiceCollection services, IConfiguration configuration)
    {
        var bff = configuration.GetSection(WebBffSettings.Section).Get<WebBffSettings>() ?? new WebBffSettings();
        var bffPublicKeys = new[] { bff.ClientPublicKeyPath }.Concat(bff.PreviousClientPublicKeyPaths)
            .Select(WebBffSettings.ReadPublicJwk)
            .OfType<string>()
            .ToArray();

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

            // The design rule: 30 minutes without activity ends the session (the same as the refresh token).
            options.Authentication.CookieLifetime = IdentityServerResources.RefreshIdleLifetime;
            options.Authentication.CookieSlidingExpiration = true;

            // Lax: the cookie goes with a normal sign-in redirect, never with a POST, image or frame from another site.
            // Duende's default (None) exists only for the check-session iframe; the BFF does not use it, so it is off.
            options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
            options.Endpoints.EnableCheckSessionEndpoint = false;
        })
        .AddInMemoryIdentityResources(IdentityServerResources.IdentityResources)
        .AddInMemoryApiScopes(IdentityServerResources.ApiScopes)
        .AddInMemoryApiResources(IdentityServerResources.ApiResources)
        .AddInMemoryClients(IdentityServerResources.Clients(WebOrigins(configuration), bff.Origins, bffPublicKeys))
        // private_key_jwt client authentication (RFC 7523), used by the BFF.
        .AddJwtBearerClientAuthentication()
        // S5: PropX sign-in rules and token claims, run by Duende on /connect/token.
        .AddResourceOwnerValidator<HarmonyPasswordValidator>()
        .AddProfileService<HarmonyProfileService>()
        // S5 two-factor: the second half of a sign-in when the account has an authenticator.
        .AddExtensionGrantValidator<MfaOtpGrantValidator>()
        // S5 step 4: refresh tokens live in SQL (Duende operational store, same database as ours), so they survive a restart and
        // a second instance sees them. Duende cleans expired and consumed rows itself (TokenCleanupHost, hourly).
        .AddOperationalStore(store =>
        {
            store.ConfigureDbContext = db => UseIdentityDatabase(db, configuration);
            store.EnableTokenCleanup = true;
            store.RemoveConsumedTokens = true;
        });

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

    /// <summary>The web app origins, read once at registration; the options validation above refuses a bad list at startup.</summary>
    private static string[] WebOrigins(IConfiguration configuration)
        => configuration.GetSection($"{WebLinkSettings.Section}:{nameof(WebLinkSettings.AllowedOrigins)}").Get<string[]>() ?? [];

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