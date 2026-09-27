namespace Harmony.Identity.Handler.Commands.RoleModule.Edit;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Repositories;

public class EditRoleCommandHandler : RoleCommandHandlerBase, IRequestHandler<EditRoleCommand, CallResponse>
{
    public EditRoleCommandHandler(IRoleRepository roleRepository)
        : base(roleRepository)
    {
    }

    public async Task<CallResponse> Handle(EditRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await this.RoleRepository.GetByIdAsync(command.Id, cancellationToken);

        if (role is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.RoleNotFound));
        }

        // Only touch what changed, so renaming a system role is not refused for a code it kept.
        // Domain rules first (system role, format, reserved), then uniqueness, same order as create.
        if (!IdentityText.SameCode(role.Code, command.Code))
        {
            role.ChangeCode(command.Code);

            if (await this.IsCodeInUseAsync(role.Code, excludeId: role.Id, cancellationToken))
            {
                return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.RoleCodeInUse));
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