namespace Harmony.Identity.Handler.Commands.RoleModule.Permissions;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Repositories;

public class GrantRolePermissionCommandHandler : RoleCommandHandlerBase, IRequestHandler<GrantRolePermissionCommand, CallResponse>
{
    public GrantRolePermissionCommandHandler(IRoleRepository roleRepository)
        : base(roleRepository)
    {
    }

    public async Task<CallResponse> Handle(GrantRolePermissionCommand command, CancellationToken cancellationToken)
    {
        var role = await this.RoleRepository.GetByIdAsync(command.RoleId, cancellationToken);

        if (role is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.RoleNotFound));
        }

        role.Grant(command.PermissionCode);

        await this.RoleRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}