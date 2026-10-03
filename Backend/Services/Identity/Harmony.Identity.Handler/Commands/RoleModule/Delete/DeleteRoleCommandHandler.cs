namespace Harmony.Identity.Handler.Commands.RoleModule.Delete;

using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Core.Identity.Permissions;

public class DeleteRoleCommandHandler : RoleCommandHandlerBase, IRequestHandler<DeleteRoleCommand, CallResponse>
{
    private readonly ICacheService cacheService;

    public DeleteRoleCommandHandler(IRoleRepository roleRepository, ICacheService cacheService)
        : base(roleRepository)
    {
        this.cacheService = cacheService;
    }

    public async Task<CallResponse> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await this.RoleRepository.GetByIdAsync(command.Id, cancellationToken);

        if (role is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Role.NotFound));
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
