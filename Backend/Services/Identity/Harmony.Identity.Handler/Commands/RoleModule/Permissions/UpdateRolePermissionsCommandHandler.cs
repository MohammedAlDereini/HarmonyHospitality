namespace Harmony.Identity.Handler.Commands.RoleModule.Permissions;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Core.Identity.Permissions;

public class UpdateRolePermissionsCommandHandler : IRequestHandler<UpdateRolePermissionsCommand, CallResponse>
{
    private readonly IRoleRepository roles;
    private readonly IPermissionRepository permissions;
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;
    private readonly ICacheService cacheService;

    public UpdateRolePermissionsCommandHandler(IRoleRepository roles, IPermissionRepository permissions, IUserAccountRepository userAccounts, ICallContext callContext, ICacheService cacheService)
    {
        this.roles = roles;
        this.permissions = permissions;
        this.userAccounts = userAccounts;
        this.callContext = callContext;
        this.cacheService = cacheService;
    }

    public async Task<CallResponse> Handle(UpdateRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        // 1. The role must exist (tracked, with its rows).
        var role = await this.roles.GetWithPermissionsAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Fail(BusinessErrorCodes.Identity.Role.NotFound);
        }

        // A system role (SUPERADMIN, a tenant's ADMINISTRATOR) is born with the tenant and is nobody's to re-permission.
        if (role.IsSystemRole)
        {
            return Fail(BusinessErrorCodes.Identity.Role.SystemRoleImmutable);
        }

        // The rank rule: what a role standing above the caller may do is not the caller's to change.
        var callerRank = await this.userAccounts.RankOfAsync(RankRules.CallerId(this.callContext), cancellationToken);
        if (RoleRank.Of(role.PrivilegeLevel, role.IsSuperRole) > callerRank)
        {
            return Fail(BusinessErrorCodes.Identity.Rank.HigherRank);
        }

        // 2. Every requested permission must exist in this tenant and be active.
        var desired = command.PermissionIds.Distinct().ToList();
        var known = await this.permissions.Query()
            .Where(p => desired.Contains(p.Id) && p.IsActive)
            .Select(p => new { p.Id, p.PermissionValue })
            .ToListAsync(cancellationToken);

        if (known.Count != desired.Count)
        {
            return Fail(BusinessErrorCodes.Identity.Permission.Unknown);
        }

        // 3. Diff and save; the aggregate refuses system roles.
        role.UpdatePermissions(desired);
        await this.roles.SaveChangesAsync(cancellationToken);

        // 4. The cache follows the database at once.
        await this.cacheService.AddAsync(
            PermissionCacheKeys.RolePermissionKey(role.TenantId, role.Id),
            known.Select(p => (int)p.PermissionValue).Distinct().Order().ToList(),
            PermissionCacheKeys.RolePermissionsLifetime,
            cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }

    private static CallResponse Fail(string errorCode)
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));
}
