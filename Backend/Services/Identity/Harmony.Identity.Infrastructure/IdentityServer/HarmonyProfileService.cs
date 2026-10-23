using System.Globalization;
using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// Duende's profile service: what a Harmony token says about its subject. The claims are
/// exactly the ones every service reads: tenant_id, role (one claim per role id), security_version, name, and
/// must_change_password while the person still has to replace a password somebody else chose.
/// Duende asks again on every refresh, and <see cref="IsActiveAsync"/> is where a session ends: a suspended or
/// terminated account cannot refresh, and neither can a refresh token issued before the SecurityVersion was bumped
/// (password changed, roles changed, suspended): that session is over and the person signs in again.
/// </summary>
public sealed class HarmonyProfileService : IProfileService
{
    private readonly IdentityDbContext dbContext;
    private readonly PlatformTenantSettings platformTenant;

    public HarmonyProfileService(IdentityDbContext dbContext, IOptions<PlatformTenantSettings> platformTenant)
    {
        this.dbContext = dbContext;
        this.platformTenant = platformTenant.Value;
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

        if (user.MustChangePassword)
        {
            claims.Add(new Claim(PlatformClaimTypes.MustChangePassword, "1"));
        }

        // Only what the requested scopes allow through (the harmony API resource lists these claim types).
        context.AddRequestedClaims(claims);
    }

    public async Task IsActiveAsync(IsActiveContext context, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(context.Subject, cancellationToken);

        // The account, then its company: a suspended or terminated tenant gets no token and no refresh, so every session
        // in it ends within the access token's life.
        var active = user is not null && user.CanSignIn
            && await this.dbContext.Tenants.AnyAsync(t => t.Id == user.TenantId && t.Status == TenantStatus.Active, cancellationToken);

        // Duende asks per connected application (v8: Application, an OIDC client or a SAML service provider). A platform-only
        // client (the console) issues nothing to a hotel account: no code, no token, no refresh. At the authorize endpoint
        // "not active" means "sign in again", and the sign-in page then says why.
        if (active && context.Application is Client client && IdentityServerResources.IsPlatformOnly(client))
        {
            active = user!.TenantId == this.platformTenant.TenantId;
        }

        // Every session remembers the version it started with: the refresh token (the grants put it on the subject), the
        // sign-in cookie and the one-time code (the sign-in pages put it there). A bump since then (password changed,
        // roles changed, signed out everywhere, suspended) ends that session: no refresh, no code, the sign-in page again.
        if (active && context.Caller is IdentityServerConstants.ProfileIsActiveCallers.RefreshTokenValidation
                or IdentityServerConstants.ProfileIsActiveCallers.AuthorizeEndpoint
                or IdentityServerConstants.ProfileIsActiveCallers.AuthorizationCodeValidation)
        {
            var issuedWith = context.Subject.FindFirst(PlatformClaimTypes.SecurityVersion)?.Value;
            active = int.TryParse(issuedWith, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
                && version == user!.SecurityVersion;
        }

        context.IsActive = active;
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
