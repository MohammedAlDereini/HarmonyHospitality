namespace Harmony.Identity.Handler.Commands.LookupModule.Categories;

using Harmony.Identity.Domain.Repositories;

public abstract class LookupCategoryCommandHandlerBase
{
    protected LookupCategoryCommandHandlerBase(ILookupCategoryRepository lookupCategoryRepository)
    {
        this.LookupCategoryRepository = lookupCategoryRepository;
    }

    protected ILookupCategoryRepository LookupCategoryRepository { get; }

    protected async Task<bool> IsNameInUseAsync(string normalizedName, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await this.LookupCategoryRepository.AnyAsync(
            c => c.NormalizedName == normalizedName
                 && (excludeId == null || c.Id != excludeId),
            cancellationToken);
    }
}