namespace Harmony.Identity.Handler.Commands.TenantModule;

using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// The one write the platform ever makes inside a customer: the birth of its administrator. A customer tenant is born with
/// the tenant-scope permission catalogue, one system role ADMINISTRATOR (level Administrator, every tenant permission,
/// the way Mews's first user and Opera's CODE-ADMIN group are born), and one account holding it, invited by e-mail with a
/// one-time 24-hour link. The work runs under the NEW tenant's id for exactly as long as it takes, and the caller's own
/// tenant is put back in a finally: the platform never stays inside a customer. It is idempotent: whatever already exists
/// is kept, whatever is missing is made, so the same step repairs a half-born tenant and re-sends an expired invitation.
/// It stops the moment the administrator has chosen a password (00086): from then on the tenant runs itself.
/// </summary>
public abstract class TenantAdministratorHandlerBase : TenantCommandHandlerBase
{
    private const string AdministratorNameEn = "Administrator";
    private const string AdministratorNameAr = "المدير";

    private readonly IPermissionRepository permissions;
    private readonly IRoleRepository roles;
    private readonly UserManager<User> userManager;
    private readonly IAccountMailer mailer;
    private readonly ICacheService cacheService;
    private readonly ILogger logger;

    protected TenantAdministratorHandlerBase(
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        IPermissionRepository permissions,
        IRoleRepository roles,
        UserManager<User> userManager,
        IAccountMailer mailer,
        ICacheService cacheService,
        ILogger logger)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.permissions = permissions;
        this.roles = roles;
        this.userManager = userManager;
        this.mailer = mailer;
        this.cacheService = cacheService;
        this.logger = logger;
    }

    /// <summary>What the step ended with: the mail went out, or it did not and the trail says so (the door can be called again).</summary>
    protected enum Invitation
    {
        Sent,
        NotSent,
    }

    /// <summary>
    /// One e-mail is one person on the whole platform (decided 2026-10-16): an address held by an account in ANOTHER tenant
    /// cannot become this tenant's administrator. Checked before anything is written, so a refusal leaves nothing behind.
    /// </summary>
    protected async Task<bool> AdministratorEmailTakenElsewhereAsync(Guid tenantId, string email, CancellationToken cancellationToken)
    {
        var holders = await this.UserAccounts.FindByUserNameAcrossTenantsAsync(this.userManager.NormalizeName(email), cancellationToken);
        return holders.Any(holder => holder.TenantId != tenantId);
    }

    /// <summary>The account holding the tenant's ADMINISTRATOR role, when both exist; null while the tenant is not born yet.</summary>
    protected async Task<User?> ExistingAdministratorAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var role = await this.roles.FindInTenantByCodeAsync(tenantId, SystemRoles.Administrator, cancellationToken);
        return role is null ? null : await this.UserAccounts.FindWithRoleInTenantAsync(tenantId, role.Id, cancellationToken);
    }

    protected async Task<CallResponse<Invitation>> ProvisionAdministratorAsync(Domain.Entities.Aggregates.TenantModule.Tenant tenant, string email, string displayName, CancellationToken cancellationToken)
    {
        if (await this.AdministratorEmailTakenElsewhereAsync(tenant.Id, email, cancellationToken))
        {
            return Fail<Invitation>(BusinessErrorCodes.Identity.Tenant.AdministratorEmailTaken);
        }

        var admin = await this.ExistingAdministratorAsync(tenant.Id, cancellationToken);
        if (admin?.PasswordHash is not null)
        {
            return Fail<Invitation>(BusinessErrorCodes.Identity.Tenant.AdministratorAlreadyActive);
        }

        // Inside the customer, for the rows below only. The caller's tenant comes back whatever happens.
        var callerTenant = this.CallContext.TenantId();
        this.CallContext.SetTenantId(tenant.Id.ToString());
        Role role;
        List<int> values;
        try
        {
            // The catalogue: the tenant-scope lines the tenant does not have yet.
            var existing = await this.permissions.OfTenantAsync(tenant.Id, cancellationToken);
            var missing = PermissionCatalogue.For(platformTenant: false)
                .Where(entry => existing.All(row => row.PermissionValue != entry.Value))
                .ToList();
            var added = await this.permissions.AddCatalogueAsync(missing, cancellationToken);
            var catalogue = existing.Concat(added).ToList();
            values = catalogue.Select(row => (int)row.PermissionValue).Distinct().Order().ToList();

            // The role: one system role with every tenant permission; a system role is never edited or deleted.
            role = await this.roles.FindInTenantByCodeAsync(tenant.Id, SystemRoles.Administrator, cancellationToken)
                ?? await this.roles.AddAsync(Role.CreateSystem(SystemRoles.Administrator, AdministratorNameEn, AdministratorNameAr, PrivilegeLevel.Administrator, catalogue.Select(row => row.Id)), cancellationToken);

            if (admin is null)
            {
                // The account: no password; the invitation link is the only way in, and it dies with the first password.
                admin = User.Create(displayName, email, email);
                admin.UpdateRoles([role.Id]);

                // Identity saves here: the catalogue rows, the role and the account land in ONE SaveChanges.
                var result = await this.userManager.CreateAsync(admin);
                if (!result.Succeeded)
                {
                    var codes = result.Errors.Select(e => e.Code).ToList();
                    this.logger.LogInformation("Administrator account for tenant {Code} refused: {Codes}.", tenant.Code, string.Join(", ", codes));
                    return Fail<Invitation>(codes.Any(code => code.StartsWith("Duplicate", StringComparison.Ordinal))
                        ? BusinessErrorCodes.Identity.Tenant.AdministratorEmailTaken
                        : BusinessErrorCodes.Identity.UserAccount.StoreRefused);
                }

                await this.AuditEvents.AddAsync(this.SecurityEvents.Create(admin, SecurityAuditActions.AccountCreated), cancellationToken);
            }

            // Repairs of the catalogue or the role, and the account's own trail row, inside the tenant.
            await this.roles.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            this.CallContext.SetTenantId(callerTenant);
        }

        // The cache follows the database at once, the way CreateRole does.
        await this.cacheService.AddAsync(PermissionCacheKeys.RolePermissionKey(tenant.Id, role.Id), values, PermissionCacheKeys.RolePermissionsLifetime, cancellationToken);

        // The link: a one-time token bound to the account's security stamp, 24 hours (Identity's default), dead with the first password.
        var token = await this.userManager.GeneratePasswordResetTokenAsync(admin);
        Invitation outcome;
        try
        {
            await this.mailer.SendTenantAdministratorInvitationAsync(admin, tenant.LegalNameEn, token, cancellationToken);
            await this.TrailAsync(SecurityAuditActions.TenantAdministratorInvited, tenant, cancellationToken);
            outcome = Invitation.Sent;
        }
        catch (Exception exception)
        {
            // The tenant and its administrator exist; only the mail is missing. The trail says so and the door can be called again.
            this.logger.LogWarning(exception, "The administrator invitation for tenant {Code} could not be sent; call InviteAdministrator again.", tenant.Code);
            await this.TrailAsync(SecurityAuditActions.TenantAdministratorInvitationFailed, tenant, cancellationToken);
            outcome = Invitation.NotSent;
        }

        await this.Tenants.SaveChangesAsync(cancellationToken);
        this.logger.LogInformation("Tenant {Code} administrator {Outcome} (role {RoleId}).", tenant.Code, outcome, role.Id);
        return CallResponseBuilder.CreateResponse<Invitation>(eCallResponseStatus.Success).HasData(outcome);
    }
}
