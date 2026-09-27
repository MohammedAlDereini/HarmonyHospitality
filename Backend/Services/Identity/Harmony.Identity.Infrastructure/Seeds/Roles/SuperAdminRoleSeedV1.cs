using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Aggregates.RoleModule;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Harmony.Identity.Infrastructure.Seeds.Roles;

internal sealed class SuperAdminRoleSeedV1 : IDbSeed<IdentityDbContext>
{
    public int Version => 1;

    public async Task SeedAsync(IdentityDbContext dbContext, Tenant tenant, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var platformTenantId = configuration["ApplicationSettings:ApplicationKeys:PlatformTenantId"]
            ?? throw new InvalidOperationException("ApplicationSettings:ApplicationKeys:PlatformTenantId is not configured.");

        // Only the company's own tenant gets the super role. Hotel tenants never do.
        if (!string.Equals(tenant.Id, platformTenantId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var tenantId = new Guid(tenant.Id);

        var exists = await dbContext.Roles
            .IgnoreQueryFilters()
            .AnyAsync(r => r.TenantId == tenantId && r.Code == SystemRoles.SuperAdmin, cancellationToken);

        if (exists)
        {
            return;
        }

        await dbContext.Roles.AddAsync(Role.CreateSuper(SystemRoles.SuperAdmin, "Super Admin", "المدير الأعلى"), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}