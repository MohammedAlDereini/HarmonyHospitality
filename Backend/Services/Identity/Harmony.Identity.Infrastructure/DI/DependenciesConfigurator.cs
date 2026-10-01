using Harmony.Core.DI;
using Harmony.Core.Enums;
using Harmony.Core.Identity.DI;
using Harmony.Core.Localization.DI;
using Harmony.Core.Logging.DI;
using Harmony.Core.Notification.DI;
using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Repositories;
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
using Duende.IdentityServer.Stores;
using Harmony.Identity.Infrastructure.IdentityServer;
namespace Harmony.Identity.Infrastructure.DI;


public static class DependenciesConfigurator
{
    public static void AddInfrastructureService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuildingBlocks(configuration);

        // Needs RabbitMQ + Redis; remove until the event bus is wired.
        services.RemoveAll<IIntegrationEventHandler<SystemEvent>>();

        services.AddCore(configuration);
        services.AddTokenSigning(configuration);
        services.AddScoped<ILookupCategoryRepository, LookupCategoryRepository>();
        services.AddScoped<ILookupValueRepository, LookupValueRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddUserAccounts();
        services.AddIdentityServerHost(configuration);
    }

    /// <summary>Duende's endpoints (discovery, jwks, token). After routing, before the controllers.</summary>
    public static void ConfigureIdentityServer(this IApplicationBuilder app)
    {
        app.UseIdentityServer();
    }

    /// <summary>
    /// Duende IdentityServer as the token engine. It signs with our key ring (S3a) — automatic key
    /// management is off — and issues service tokens through the harmony:service grant. No licence key:
    /// in non-production Duende runs in trial mode and only logs a notice. Production needs a licence.
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
        .AddExtensionGrantValidator<ServicePrincipalGrantValidator>();

        // Registered after AddIdentityServer so these win over Duende's automatic key stores.
        services.AddSingleton(sp => KeyRingStores.Signing(sp.GetRequiredService<RsaSigningKeyProvider>()));
        services.AddSingleton(sp => KeyRingStores.Validation(sp.GetRequiredService<RsaSigningKeyProvider>()));
    }

    public static void ConfigureAppLogging(this IApplicationBuilder app)
    {
        app.UseCore(coreBuilderConfig =>
        {
            coreBuilderConfig.UseExceptionHandling();
            coreBuilderConfig.UseSwagger();
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
            config.AddReadOnlyRepository<HarmonyUser, IdentityDbContext>();
            config.AddMediatR(mediatRConfig =>
            {
                mediatRConfig.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
                mediatRConfig.AddValidationBehavior();
            });
        });
    }

    /// <summary>
    /// The signing keys, checked at start: a bad issuer or key stops the service before it signs anything.
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
    /// ASP.NET Core Identity for users, stored in OUR DbContext: UserOnlyStore needs only a DbContext
    /// that maps Identity's four tables, so the framework's tenant filter, audit and rowversion apply
    /// to users too. No Identity roles: Harmony's Role aggregate carries the rules Identity's roles lack.
    /// </summary>
    private static void AddUserAccounts(this IServiceCollection services)
    {
        // Fully qualified: Identity's package also has a type named IdentityDbContext.
        services.AddIdentityCore<HarmonyUser>()
            .AddUserStore<Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserOnlyStore<HarmonyUser, IdentityDbContext, Guid>>();
    }


    private static void AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        var tenantId = configuration["ApplicationSettings:ApplicationKeys:PlatformTenantId"]!;

        Harmony.Core.DI.DependenciesConfigurator.AddCore(services, configuration, coreServicesConfig =>
        {
            coreServicesConfig.AddSwagger(conf =>
            {
                conf.AddHeader(Harmony.Core.Constants.ContextValues.TenantId, isRequired: true, defaultValue: tenantId);
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
}