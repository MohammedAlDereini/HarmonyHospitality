namespace Harmony.Identity.Handler.Commands.RoleModule;

using Harmony.Identity.Domain.Repositories;

public abstract class RoleCommandHandlerBase
{
    protected RoleCommandHandlerBase(IRoleRepository roleRepository)
    {
        this.RoleRepository = roleRepository;
    }

    protected IRoleRepository RoleRepository { get; }

    protected async Task<bool> IsCodeInUseAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await this.RoleRepository.AnyAsync(
            r => r.Code == code
                 && (excludeId == null || r.Id != excludeId),
            cancellationToken);
    }
}