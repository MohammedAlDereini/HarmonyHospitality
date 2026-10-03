namespace Harmony.Identity.Handler.Commands.LookupModule.Categories.Delete;

using Harmony.Identity.Domain.Repositories;

public class DeleteLookupCategoryCommandHandler : IRequestHandler<DeleteLookupCategoryCommand, CallResponse>
{
    private readonly ILookupCategoryRepository lookupCategoryRepository;

    public DeleteLookupCategoryCommandHandler(ILookupCategoryRepository lookupCategoryRepository)
    {
        this.lookupCategoryRepository = lookupCategoryRepository;
    }

    public async Task<CallResponse> Handle(DeleteLookupCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await this.lookupCategoryRepository.GetByIdAsync(command.Id, cancellationToken);

        if (category is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Lookup.CategoryNotFound));
        }

        category.Delete();

        await this.lookupCategoryRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}