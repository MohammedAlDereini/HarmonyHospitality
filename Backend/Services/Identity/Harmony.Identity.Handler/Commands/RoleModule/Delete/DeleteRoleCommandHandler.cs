namespace Harmony.Identity.Handler.Commands.RoleModule.Delete;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Repositories;

public class DeleteRoleCommandHandler : RoleCommandHandlerBase, IRequestHandler<DeleteRoleCommand, CallResponse>
{
    public DeleteRoleCommandHandler(IRoleRepository roleRepository)
        : base(roleRepository)
    {
    }

    public async Task<CallResponse> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await this.RoleRepository.GetByIdAsync(command.Id, cancellationToken);

        if (role is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.RoleNotFound));
        }

        role.AssertDeletable();

        await this.RoleRepository.DeleteAsync(role, cancellationToken);
        await this.RoleRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}