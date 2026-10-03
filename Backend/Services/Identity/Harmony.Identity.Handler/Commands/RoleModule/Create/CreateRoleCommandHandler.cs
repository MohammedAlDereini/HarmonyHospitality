namespace Harmony.Identity.Handler.Commands.RoleModule.Create;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Repositories;

public class CreateRoleCommandHandler : RoleCommandHandlerBase, IRequestHandler<CreateRoleCommand, CallResponse<Guid>>
{
    private readonly IPermissionRepository permissions;

    public CreateRoleCommandHandler(IRoleRepository roleRepository, IPermissionRepository permissions)
        : base(roleRepository)
    {
        this.permissions = permissions;
    }

    public async Task<CallResponse<Guid>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var role = Role.Create(command.Code, command.NameEn, command.NameAr, command.PrivilegeLevel);

        if (await this.IsCodeInUseAsync(role.Code, excludeId: null, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.RoleCodeInUse));
        }

        // Every requested permission must exist in this tenant and be active.
        var desired = command.PermissionIds.Distinct().ToList();
        if (desired.Count > 0)
        {
            var known = await this.permissions.Query()
                .Where(p => desired.Contains(p.Id) && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            if (known.Count != desired.Count)
            {
                return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.UnknownPermission));
            }

            role.UpdatePermissions(desired);
        }

        await this.RoleRepository.AddAsync(role, cancellationToken);
        await this.RoleRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(role.Id);
    }
}
