using Harmony.Core.DI;
using Harmony.Core.Enums;
using Harmony.Core.Exceptions;
using Harmony.Core.Identity.DI;
using Harmony.Core.Localization.DI;
using Harmony.Core.Logging.DI;
using Harmony.Core.Notification.DI;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Identity.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Harmony.Core.EventBus.Abstractions;
using Harmony.Core.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Infrastructure.Signing;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Infrastructure.Caching;
using Harmony.Identity.Infrastructure.Services;
using Harmony.Identity.Infrastructure.IdentityServer;
using System.IO;
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
            .AddUserStore<Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserOnlyStore<User, IdentityDbContext, Guid>>();
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
        services.AddIdentityServer(options =>
        {
            options.IssuerUri = configuration[$"{TokenIssuerSettings.Section}:{nameof(TokenIssuerSettings.Issuer)}"];
            options.KeyManagement.Enabled = false;
            options.EmitStaticAudienceClaim = false;
        })
        .AddInMemoryApiScopes(IdentityServerResources.ApiScopes)
        .AddInMemoryApiResources(IdentityServerResources.ApiResources)
        .AddInMemoryClients(IdentityServerResources.Clients)
        // S5: PropX sign-in rules and token claims, run by Duende on /connect/token.
        .AddResourceOwnerValidator<HarmonyPasswordValidator>()
        .AddProfileService<HarmonyProfileService>();

        // Registered after AddIdentityServer so these win over Duende's automatic key stores.
        services.AddSingleton(sp => KeyRingStores.Signing(sp.GetRequiredService<RsaSigningKeyProvider>()));
        services.AddSingleton(sp => KeyRingStores.Validation(sp.GetRequiredService<RsaSigningKeyProvider>()));
    }
}