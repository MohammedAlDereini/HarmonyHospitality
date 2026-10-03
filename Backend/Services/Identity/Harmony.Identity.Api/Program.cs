using Harmony.Core.BuildingBlocks;
using Harmony.Identity.Api.Checks;
using Harmony.Identity.Handler.DI;
using Harmony.Identity.Infrastructure.Caching;
using Harmony.Identity.Infrastructure.DI;
using Harmony.Identity.Infrastructure.Persistence;

/// <summary>
/// Entry point class for the Harmony Identity API application.
/// </summary>
public partial class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureAppSettings(builder);
        ConfigureAppServices(builder);
        ConfigureMvc(builder);

        var app = builder.Build();

        app.MigrateDbContext<IdentityDbContext, IdentityDbContextSeed>();

        // Every tenant must hold a row for every PermissionEnum value, or the service does not start.
        await PermissionEnumIntegrityCheck.ValidateAsync(app);

        await ConfigureCacheSeedingAsync(app);

        ConfigureApp(app);

        await app.RunAsync();

        static void ConfigureMvc(WebApplicationBuilder builder)
        {
            builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);
        }

        static void ConfigureAppSettings(WebApplicationBuilder builder)
        {
            builder.Configuration.ConfigureApplicationSettings(builder.Environment);
            builder.Configuration.ConfigureInfrastructureSettings(builder.Environment);
        }

        static void ConfigureAppServices(WebApplicationBuilder builder)
        {
            builder.Services.AddApplicationService(builder.Configuration);
            builder.Services.AddInfrastructureService(builder.Configuration);
        }

        static async Task ConfigureCacheSeedingAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<ICacheSeeder>();
            await seeder.InitializeAsync();
        }

        static void ConfigureApp(WebApplication app)
        {
            app.UseRouting();
            app.ConfigureAppLogging();
            app.MapControllers();
        }
    }
}

/// <summary>
/// Entry point class for the Harmony Identity API application.
/// </summary>
public partial class Program
{
}