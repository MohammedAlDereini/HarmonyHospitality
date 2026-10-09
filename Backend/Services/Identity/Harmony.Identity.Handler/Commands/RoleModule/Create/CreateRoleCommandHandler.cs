namespace Harmony.Identity.Handler.Commands.RoleModule.Create;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Core.Identity.Permissions;

public class CreateRoleCommandHandler : RoleCommandHandlerBase, IRequestHandler<CreateRoleCommand, CallResponse<Guid>>
{
    private readonly IPermissionRepository permissions;
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;
    private readonly ICacheService cacheService;

    public CreateRoleCommandHandler(IRoleRepository roleRepository, IPermissionRepository permissions, IUserAccountRepository userAccounts, ICallContext callContext, ICacheService cacheService)
        : base(roleRepository)
    {
        this.permissions = permissions;
        this.userAccounts = userAccounts;
        this.callContext = callContext;
        this.cacheService = cacheService;
    }

    public async Task<CallResponse<Guid>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var role = Role.Create(command.Code, command.NameEn, command.NameAr, command.PrivilegeLevel);

        // The rank rule: nobody builds a role above their own standing.
        var callerRank = await this.userAccounts.RankOfAsync(RankRules.CallerId(this.callContext), cancellationToken);
        if (RoleRank.Of(role.PrivilegeLevel, isSuperRole: false) > callerRank)
        {
            return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Rank.AboveSelf));
        }

        if (await this.IsCodeInUseAsync(role.Code, excludeId: null, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Role.CodeInUse));
        }

        // Every requested permission must exist in this tenant and be active.
        var desired = command.PermissionIds.Distinct().ToList();
        var values = new List<int>();
        if (desired.Count > 0)
        {
            var known = await this.permissions.Query()
                .Where(p => desired.Contains(p.Id) && p.IsActive)
                .Select(p => new { p.Id, p.PermissionValue })
                .ToListAsync(cancellationToken);

            if (known.Count != desired.Count)
            {
                return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Permission.Unknown));
            }

            role.UpdatePermissions(desired);
            values = known.Select(p => (int)p.PermissionValue).Distinct().Order().ToList();
        }

        await this.RoleRepository.AddAsync(role, cancellationToken);
        await this.RoleRepository.SaveChangesAsync(cancellationToken);

        // The cache follows the database at once.
        await this.cacheService.AddAsync(
            PermissionCacheKeys.RolePermissionKey(role.TenantId, role.Id),
            values,
            PermissionCacheKeys.RolePermissionsLifetime,
            cancellationToken);

        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(role.Id);
    }
}
