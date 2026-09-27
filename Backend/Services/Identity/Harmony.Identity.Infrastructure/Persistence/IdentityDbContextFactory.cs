using Harmony.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Harmony.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("applicationSettings.json", optional: false)
            .AddJsonFile($"applicationSettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
            .Build();

        var dbEngine = configuration["ApplicationSettings:ApplicationKeys:DefaultDbEngine"];
        var connectionString = configuration["ApplicationSettings:ConnectionStrings:Default"];

        var builder = new DbContextOptionsBuilder<IdentityDbContext>();

        if (string.Equals(dbEngine, nameof(eDbEngine.PostgreSql), StringComparison.OrdinalIgnoreCase))
        {
            builder.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(60), null);
                npgsql.CommandTimeout(300);
            });
        }
        else
        {
            builder.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(60), null);
                sql.CommandTimeout(300);
            });
        }

        return new IdentityDbContext(builder.Options);
    }
}