using Harmony.Core.BuildingBlocks.DI;
using Harmony.Core.DI;
using Harmony.Core.Validation.DI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Harmony.Identity.Handler.DI;

public static class DependenciesConfigurator
{
    public static void AddApplicationService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidations(configuration);
    }

    public static void ConfigureApplicationSettings(this IConfigurationBuilder configurationBuilder, IHostEnvironment hostingEnvironment)
    {
        configurationBuilder.ConfigureCoreConfiguration(hostingEnvironment);
        configurationBuilder.ConfigureBuildingBlocksConfiguration(hostingEnvironment);
        configurationBuilder.ConfigureValidationConfiguration(hostingEnvironment);
    }

    private static void AddValidations(this IServiceCollection services, IConfiguration configuration)
    {
        Harmony.Core.Validation.DI.DependenciesConfigurator.AddValidation(services, configuration, validationServicesConfig =>
        {
            validationServicesConfig.RegisterValidatorsFromAssemblyContaining<ApplicationClass>();
            validationServicesConfig.AddFluentValidationFilter();
        });
    }
}