namespace Harmony.Identity.Handler.Commands.RoleModule.Edit;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Repositories;

public class EditRoleCommandHandler : RoleCommandHandlerBase, IRequestHandler<EditRoleCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;

    public EditRoleCommandHandler(IRoleRepository roleRepository, IUserAccountRepository userAccounts, ICallContext callContext)
        : base(roleRepository)
    {
        this.userAccounts = userAccounts;
        this.callContext = callContext;
    }

    public async Task<CallResponse> Handle(EditRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await this.RoleRepository.GetByIdAsync(command.Id, cancellationToken);

        if (role is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Role.NotFound));
        }

        // The rank rules: a role standing above the caller is not theirs to shape; its level may never rise above
        // the caller; and the one role every active administrator stands on may not sink below Administrator.
        var callerRank = await this.userAccounts.RankOfAsync(RankRules.CallerId(this.callContext), cancellationToken);
        if (RoleRank.Of(role.PrivilegeLevel, role.IsSuperRole) > callerRank)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Rank.HigherRank));
        }

        if (RoleRank.Of(command.PrivilegeLevel, isSuperRole: false) > callerRank)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Rank.AboveSelf));
        }

        if (role.PrivilegeLevel == PrivilegeLevel.Administrator
            && command.PrivilegeLevel != PrivilegeLevel.Administrator
            && await this.userAccounts.SoleAdministratorRoleAsync(role.Id, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Rank.LastAdministrator));
        }

        // Only touch what changed, so renaming a system role is not refused for a code it kept.
        // Domain rules first (system role, format, reserved), then uniqueness, same order as create.
        if (!IdentityText.SameCode(role.Code, command.Code))
        {
            role.ChangeCode(command.Code);

            if (await this.IsCodeInUseAsync(role.Code, excludeId: role.Id, cancellationToken))
            {
                return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Role.CodeInUse));
            }
        }

        if (role.PrivilegeLevel != command.PrivilegeLevel)
        {
            role.SetPrivilegeLevel(command.PrivilegeLevel);
        }

        role.Rename(command.NameEn, command.NameAr);

        await this.RoleRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}