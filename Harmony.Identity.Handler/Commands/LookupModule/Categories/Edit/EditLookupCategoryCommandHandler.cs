namespace Harmony.Identity.Handler.Commands.LookupModule.Categories.Edit;

using Harmony.Identity.Domain.Aggregates.LookupModule;
using Harmony.Identity.Domain.Repositories;

public class EditLookupCategoryCommandHandler : LookupCategoryCommandHandlerBase, IRequestHandler<EditLookupCategoryCommand, CallResponse>
{
    public EditLookupCategoryCommandHandler(ILookupCategoryRepository lookupCategoryRepository)
        : base(lookupCategoryRepository)
    {
    }

    public async Task<CallResponse> Handle(EditLookupCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await this.LookupCategoryRepository.GetByIdAsync(command.Id, cancellationToken);

        if (category is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New("00002"));
        }

        var name = command.Name.Trim();
        var normalized = LookupCategory.Normalize(name);

        if (await this.IsNameInUseAsync(normalized, excludeId: category.Id, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New("00001"));
        }

        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();

        category.Update(name, description);

        await this.LookupCategoryRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}