namespace Harmony.Identity.Handler.Commands.RoleModule.Create;

using Harmony.Identity.Domain.Aggregates.RoleModule;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Repositories;

public class CreateRoleCommandHandler : RoleCommandHandlerBase, IRequestHandler<CreateRoleCommand, CallResponse<Guid>>
{
    public CreateRoleCommandHandler(IRoleRepository roleRepository)
        : base(roleRepository)
    {
    }

    public async Task<CallResponse<Guid>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var role = Role.Create(command.Code, command.NameEn, command.NameAr, command.PrivilegeLevel);

        foreach (var permissionCode in command.PermissionCodes)
        {
            role.Grant(permissionCode);
        }

        if (await this.IsCodeInUseAsync(role.Code, excludeId: null, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.RoleCodeInUse));
        }

        await this.RoleRepository.AddAsync(role, cancellationToken);
        await this.RoleRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(role.Id);
    }
}