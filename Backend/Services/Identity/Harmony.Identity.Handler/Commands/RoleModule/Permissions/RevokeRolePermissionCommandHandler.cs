namespace Harmony.Identity.Handler.Commands.RoleModule.Permissions;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Repositories;

public class RevokeRolePermissionCommandHandler : RoleCommandHandlerBase, IRequestHandler<RevokeRolePermissionCommand, CallResponse>
{
    public RevokeRolePermissionCommandHandler(IRoleRepository roleRepository)
        : base(roleRepository)
    {
    }

    public async Task<CallResponse> Handle(RevokeRolePermissionCommand command, CancellationToken cancellationToken)
    {
        var role = await this.RoleRepository.GetByIdAsync(command.RoleId, cancellationToken);

        if (role is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.RoleNotFound));
        }

        role.Revoke(command.PermissionCode);

        await this.RoleRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}