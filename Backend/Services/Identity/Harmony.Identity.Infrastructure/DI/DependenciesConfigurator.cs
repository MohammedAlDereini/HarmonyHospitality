using Harmony.Core.DI;
using Harmony.Core.Enums;
using Harmony.Core.Identity.DI;
using Harmony.Core.Localization.DI;
using Harmony.Core.Logging.DI;
using Harmony.Core.Notification.DI;
using Harmony.Identity.Domain.Aggregates.LookupModule;
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
using Harmony.Identity.Domain.Aggregates.RoleModule;

namespace Harmony.Identity.Infrastructure.DI;

public static class DependenciesConfigurator
{
    public static void AddInfrastructureService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuildingBlocks(configuration);

        // Needs RabbitMQ + Redis; remove until the event bus is wired.
        services.RemoveAll<IIntegrationEventHandler<SystemEvent>>();

        services.AddCore(configuration);
        services.AddScoped<ILookupCategoryRepository, LookupCategoryRepository>();
        services.AddScoped<ILookupValueRepository, LookupValueRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
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
            config.AddMediatR(mediatRConfig =>
            {
                mediatRConfig.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
                mediatRConfig.AddValidationBehavior();
            });
        });
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