using Harmony.Core;
using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.Utilities;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Identity.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Api.Checks;

/// <summary>
/// Startup check: every tenant holds a row for every <see cref="PermissionEnum"/> value. A tenant with a hole would let
/// a role reference a permission that does not exist for it, so the service refuses to start and names the gap.
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

        foreach (var tenant in tenants)
        {
            // Same shape as the framework's seeding: a scope whose call context names the tenant.
            using var scope = app.Services.CreateScope();
            var httpContext = scope.ServiceProvider.CreateDefaultHttpContext();
            httpContext.Request.Headers[Constants.ContextValues.UserId] = "System";
            httpContext.Request.Headers[Constants.ContextValues.TenantId] = tenant.Id;
            httpContext.Request.Headers[Constants.ContextValues.CorrelationId] = Guid.CreateVersion7().ToString("N");
            scope.ServiceProvider.GetRequiredService<ICallContext>().OverrideContextHolder(httpContext);

            var tenantId = Guid.Parse(tenant.Id);
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            var present = await dbContext.Permissions
                .IgnoreQueryFilters([QueryFilterNames.Tenant])
                .Where(p => p.TenantId == tenantId)
                .Select(p => p.PermissionValue)
                .ToListAsync(cancellationToken);

            var missing = expected.Except(present).OrderBy(v => v).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"PermissionEnumIntegrityCheck: tenant {tenant.Id} is missing Permission rows for: {string.Join(", ", missing)}.");
            }
        }
    }
}
