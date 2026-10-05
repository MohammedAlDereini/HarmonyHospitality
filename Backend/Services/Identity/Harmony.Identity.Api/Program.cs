using Duende.IdentityServer.EntityFramework.DbContexts;
using Harmony.Core.BuildingBlocks;
using Harmony.Identity.Api.Checks;
using Harmony.Identity.Handler.DI;
using Harmony.Identity.Infrastructure.Caching;
using Harmony.Identity.Infrastructure.DI;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        // Duende keeps refresh tokens and Data Protection keeps its keys in our database; the framework migrates only our own context.
        await MigrateSupportingStoresAsync(app);

        // Every tenant must hold a row for every PermissionEnum value, or the service does not start.
        await PermissionEnumIntegrityCheck.ValidateAsync(app);

        // PropX: every endpoint must declare who may call it, or the service does not start.
        AuthorizationConventionCheck.Validate(app.Services);

        await ConfigureCacheSeedingAsync(app);

        ConfigureApp(app);

        await app.RunAsync();

        static void ConfigureMvc(WebApplicationBuilder builder)
        {
            builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);

            // The sign-in pages (Pages/Account): server-rendered, no script.
            builder.Services.AddRazorPages();

            // The sign-in forms' anti-forgery cookie: HTTPS only, this host only, never sent from another site, no script.
            builder.Services.AddAntiforgery(options =>
            {
                options.Cookie.Name = "__Host-harmony-antiforgery";
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.HttpOnly = true;
            });
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

        static async Task MigrateSupportingStoresAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<DataProtectionKeysDbContext>().Database.MigrateAsync();
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
            app.UseCors();
            app.ConfigureAppLogging();
            app.MapControllers();

            // wwwroot (the sign-in pages' stylesheet), fingerprinted and compressed at build time. Open to anyone: it holds nothing secret.
            app.MapStaticAssets().AllowAnonymous();

            // The sign-in pages (Pages/Account).
            app.MapRazorPages().WithStaticAssets();
        }
    }
}

/// <summary>
/// Entry point class for the Harmony Identity API application.
/// </summary>
public partial class Program
{
}