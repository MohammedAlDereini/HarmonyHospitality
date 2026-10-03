using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Core.Identity.Permissions;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Api.Checks;

/// <summary>
/// Startup check (PropX PermissionEnumIntegrityCheck): every configured tenant holds a row for every
/// <see cref="PermissionEnum"/> value. A tenant with a hole would let a role reference a permission that does not exist
/// for it, so the service refuses to start and names the gap.
/// </summary>
public static class PermissionEnumIntegrityCheck
{
    public static async Task ValidateAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        var expected = Enum.GetValues<PermissionEnum>().ToHashSet();
        var tenants = app.Services.GetRequiredService<IMultiTenancyService>().GetTenants();

        if (tenants.Count == 0)
        {
            throw new InvalidOperationException("PermissionEnumIntegrityCheck: no tenants are configured.");
        }

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // One pass over every tenant's rows: the tenant filter is dropped by name (PropX IgnoreQueryFilters()).
        var rows = await dbContext.Permissions
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .Select(p => new { p.TenantId, p.PermissionValue })
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            var tenantId = Guid.Parse(tenant.Id);
            var present = rows.Where(r => r.TenantId == tenantId).Select(r => r.PermissionValue).ToHashSet();
            var missing = expected.Except(present).OrderBy(v => v).ToList();

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"PermissionEnumIntegrityCheck: tenant {tenant.Id} is missing Permission rows for: {string.Join(", ", missing)}.");
            }
        }
    }
}
