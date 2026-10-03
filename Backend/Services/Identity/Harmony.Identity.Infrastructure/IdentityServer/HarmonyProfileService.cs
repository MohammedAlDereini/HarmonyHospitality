using System.Globalization;
using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// PropX AuthClaimsBuilder, as Duende's profile service: what a Harmony token says about its subject. The claims are
/// exactly the ones every service reads: tenant_id, role (one claim per role id), security_version and name.
/// Duende asks again on every refresh, so a changed role set or a bumped version reaches the next token without a
/// new login, and <see cref="IsActiveAsync"/> stops a suspended or terminated account from refreshing at all.
/// </summary>
public sealed class HarmonyProfileService : IProfileService
{
    private readonly IdentityDbContext dbContext;

    public HarmonyProfileService(IdentityDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(context.Subject, cancellationToken);
        if (user is null)
        {
            return;
        }

        var claims = new List<Claim>
        {
            new(PlatformClaimTypes.Tenant, user.TenantId.ToString("D")),
            new(PlatformClaimTypes.SecurityVersion, user.SecurityVersion.ToString(CultureInfo.InvariantCulture)),
            new("name", user.DisplayName),
        };
        claims.AddRange(user.UserRoles.Select(role => new Claim(PlatformClaimTypes.Role, role.RoleId.ToString("D"))));

        // Only what the requested scopes allow through (the harmony API resource lists these claim types).
        context.AddRequestedClaims(claims);
    }

    public async Task IsActiveAsync(IsActiveContext context, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(context.Subject, cancellationToken);
        context.IsActive = user is not null && user.CanSignIn;
    }

    // Runs inside Duende's token pipeline, before any tenant is known: the lookup drops the tenant filter.
    private async Task<User?> LoadAsync(ClaimsPrincipal subject, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(subject?.FindFirst("sub")?.Value, out var userId))
        {
            return null;
        }

        return await this.dbContext.UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }
}
