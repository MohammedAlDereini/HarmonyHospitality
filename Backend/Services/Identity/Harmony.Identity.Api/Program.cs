using Duende.IdentityServer.EntityFramework.DbContexts;
using Harmony.Core.BuildingBlocks;
using Harmony.Core.Middlewares.Receipts;
using Harmony.Identity.Api.Checks;
using Harmony.Identity.Handler.DI;
using Harmony.Identity.Infrastructure.Caching;
using Harmony.Identity.Infrastructure.DI;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Identity.Infrastructure.RateLimiting;
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

        // Every endpoint must declare who may call it, or the service does not start.
        AuthorizationConventionCheck.Validate(app.Services);

        await ConfigureCacheSeedingAsync(app);

        ConfigureApp(app);

        await app.RunAsync();

        static void ConfigureMvc(WebApplicationBuilder builder)
        {
            // No "Server: Kestrel" on any answer: nothing tells a caller which server software to aim at.
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

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
            // The clock the handlers compare contract dates on; a test replaces it with a fixed one.
            builder.Services.AddSingleton(TimeProvider.System);
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
            // Item 5: first, so everything after it (rate limits, the security trail) sees the caller's real address.
            app.UseForwardedHeaders();

            // Outside Development the browser is told to use HTTPS only for this host (HSTS); localhost is never included.
            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            // Every answer, the stylesheet and script included: the browser must trust the declared type, never guess one.
            app.Use((context, next) =>
            {
                context.Response.Headers.XContentTypeOptions = "nosniff";
                return next(context);
            });

            app.UseRouting();

            // Item 5: after routing, so each door's [EnableRateLimiting] is known; before any sign-in work runs. Duende's
            // token door needs no limiter of its own since BFF step 5: only the BFF (code + refresh) calls it.
            app.UseRateLimiter();

            app.ConfigureAppLogging();

            // The receipt gate: after authentication (receipts are per signed-in user), before the API endpoints.
            // A write carrying an Idempotency-Key runs once; a resent copy gets the same answer back.
            app.UseReceiptGate();
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