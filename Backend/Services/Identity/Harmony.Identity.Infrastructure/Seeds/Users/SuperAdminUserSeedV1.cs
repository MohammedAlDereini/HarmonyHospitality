using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Seeds.Users;

/// <summary>
/// The first human: one SUPERADMIN account in the company's own tenant, from IdentitySettings:SuperAdmin
/// (Email, Password, DisplayName). Created once; never touched again, so a changed setting does not reset a live
/// account. The password is hashed by Identity and never logged. A missing or short setting stops the service:
/// an Identity with nobody who can sign in is not a working Identity.
/// </summary>
internal sealed class SuperAdminUserSeedV1 : IDbSeed<IdentityDbContext>
{
    public const string Section = "IdentitySettings:SuperAdmin";
    public const int MinimumPasswordLength = PasswordRules.MinimumLength;

    public int Version => 1;

    public async Task SeedAsync(IdentityDbContext dbContext, Tenant tenant, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var platformTenantId = configuration["ApplicationSettings:ApplicationKeys:PlatformTenantId"]
            ?? throw new InvalidOperationException("ApplicationSettings:ApplicationKeys:PlatformTenantId is not configured.");

        if (!string.Equals(tenant.Id, platformTenantId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var email = configuration[$"{Section}:Email"]?.Trim();
        var password = configuration[$"{Section}:Password"];
        var displayName = configuration[$"{Section}:DisplayName"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException($"{Section}:Email and :DisplayName are required.");
        }

        if (password is null || password.Length < MinimumPasswordLength)
        {
            throw new InvalidOperationException($"{Section}:Password must be at least {MinimumPasswordLength} characters.");
        }

        var tenantId = new Guid(tenant.Id);
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var normalized = userManager.NormalizeName(email);

        var exists = await dbContext.UserAccounts
            .IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenantId && u.NormalizedUserName == normalized, cancellationToken);

        if (exists)
        {
            return;
        }

        var superRoleId = await dbContext.Roles
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.Code == SystemRoles.SuperAdmin)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

        var user = User.Create(displayName, email, email);
        user.UpdateRoles([superRoleId]);

        // The settings file chose this password; the first thing the person does is replace it.
        user.RequirePasswordChange();

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{Section}: the identity store refused the first account: {string.Join(", ", result.Errors.Select(e => e.Code))}.");
        }

        serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SuperAdminUserSeedV1).FullName!)
            .LogInformation("First SUPERADMIN account created for tenant {Tenant}: {Email}.", tenant.Id, email);
    }
}
