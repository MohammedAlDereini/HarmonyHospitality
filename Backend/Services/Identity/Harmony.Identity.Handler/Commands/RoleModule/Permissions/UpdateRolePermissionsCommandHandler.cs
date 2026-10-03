namespace Harmony.Identity.Handler.Commands.RoleModule.Permissions;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Repositories;

public class UpdateRolePermissionsCommandHandler : IRequestHandler<UpdateRolePermissionsCommand, CallResponse>
{
    private readonly IRoleRepository roles;
    private readonly IPermissionRepository permissions;

    public UpdateRolePermissionsCommandHandler(IRoleRepository roles, IPermissionRepository permissions)
    {
        this.roles = roles;
        this.permissions = permissions;
    }

    public async Task<CallResponse> Handle(UpdateRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        // 1. The role must exist (tracked, with its rows).
        var role = await this.roles.GetWithPermissionsAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Fail(IdentityErrorCodes.RoleNotFound);
        }

        // 2. Every requested permission must exist in this tenant and be active.
        var desired = command.PermissionIds.Distinct().ToList();
        var known = await this.permissions.Query()
            .Where(p => desired.Contains(p.Id) && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (known.Count != desired.Count)
        {
            return Fail(IdentityErrorCodes.UnknownPermission);
        }

        // 3. Diff and save; the aggregate refuses system roles.
        role.UpdatePermissions(desired);
        await this.roles.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }

    private static CallResponse Fail(string errorCode)
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));
}
