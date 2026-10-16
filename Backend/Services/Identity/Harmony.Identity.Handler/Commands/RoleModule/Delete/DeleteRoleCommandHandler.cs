namespace Harmony.Identity.Handler.Commands.RoleModule.Delete;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Core.Identity.Permissions;

public class DeleteRoleCommandHandler : RoleCommandHandlerBase, IRequestHandler<DeleteRoleCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;
    private readonly ICacheService cacheService;

    public DeleteRoleCommandHandler(IRoleRepository roleRepository, IUserAccountRepository userAccounts, ICallContext callContext, ICacheService cacheService)
        : base(roleRepository)
    {
        this.userAccounts = userAccounts;
        this.callContext = callContext;
        this.cacheService = cacheService;
    }

    public async Task<CallResponse> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await this.RoleRepository.GetByIdAsync(command.Id, cancellationToken);

        if (role is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Role.NotFound));
        }

        // A system role (SUPERADMIN, a tenant's ADMINISTRATOR) is born with the tenant and is nobody's to delete.
        if (role.IsSystemRole)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Role.SystemRoleImmutable));
        }

        // The rank rules: a role standing above the caller is not theirs to delete, and the one role every active
        // administrator stands on may not go at all.
        var callerRank = await this.userAccounts.RankOfAsync(RankRules.CallerId(this.callContext), cancellationToken);
        if (RoleRank.Of(role.PrivilegeLevel, role.IsSuperRole) > callerRank)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Rank.HigherRank));
        }

        if (role.PrivilegeLevel == PrivilegeLevel.Administrator
            && await this.userAccounts.SoleAdministratorRoleAsync(role.Id, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Rank.LastAdministrator));
        }

        role.AssertDeletable();

        await this.RoleRepository.DeleteAsync(role, cancellationToken);
        await this.RoleRepository.SaveChangesAsync(cancellationToken);

        // Drop any cached permission set for this role so the key doesn't linger for the TTL.
        await this.cacheService.DeleteAsync(
            PermissionCacheKeys.RolePermissionKey(role.TenantId, command.Id),
            cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
