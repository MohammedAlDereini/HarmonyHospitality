using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Seeds.Tenants;

/// <summary>
/// The platform's own Tenants row (us), written once with the id the settings name. A common seed, not a tenant seed:
/// the row is platform data. Without it the sign-in wall would refuse the platform's own people, so it runs before
/// the super admin seed can matter.
/// </summary>
internal sealed class PlatformTenantSeedV1 : IDbSeed<IdentityDbContext>
{
    public int Version => 1;

    public async Task SeedAsync(IdentityDbContext dbContext, Tenant tenant, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlatformTenantSettings>>().Value;

        if (await dbContext.Tenants.AnyAsync(t => t.Id == settings.TenantId, cancellationToken))
        {
            return;
        }

        // The one row whose id is chosen, not generated: the id every service reads from its settings.
        var row = Domain.Entities.Aggregates.TenantModule.Tenant.CreatePlatform(settings.Code, settings.LegalNameEn, settings.LegalNameAr, DateOnly.FromDateTime(DateTime.UtcNow));
        await dbContext.Tenants.AddAsync(row, cancellationToken);
        dbContext.Entry(row).Property(t => t.Id).CurrentValue = settings.TenantId;
        await dbContext.SaveChangesAsync(cancellationToken);

        serviceProvider.GetRequiredService<ILogger<PlatformTenantSeedV1>>()
            .LogInformation("Platform tenant {Code} seeded as {TenantId}.", settings.Code, settings.TenantId);
    }
}
