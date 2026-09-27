namespace Harmony.Identity.Handler.Commands.LookupModule.Values;

using Harmony.Identity.Domain.Repositories;

public abstract class LookupValueCommandHandlerBase
{
    protected LookupValueCommandHandlerBase(ILookupValueRepository lookupValueRepository)
    {
        this.LookupValueRepository = lookupValueRepository;
    }

    protected ILookupValueRepository LookupValueRepository { get; }

    protected async Task<bool> IsNameInUseInCategoryAsync(Guid lookupCategoryId, string normalizedName, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await this.LookupValueRepository.AnyAsync(
            v => v.LookupCategoryId == lookupCategoryId
                 && v.NormalizedName == normalizedName
                 && (excludeId == null || v.Id != excludeId),
            cancellationToken);
    }
}