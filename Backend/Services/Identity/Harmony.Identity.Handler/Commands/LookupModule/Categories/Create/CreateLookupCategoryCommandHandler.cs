namespace Harmony.Identity.Handler.Commands.LookupModule.Categories.Create;

using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Repositories;

public class CreateLookupCategoryCommandHandler : LookupCategoryCommandHandlerBase, IRequestHandler<CreateLookupCategoryCommand, CallResponse>
{
    public CreateLookupCategoryCommandHandler(ILookupCategoryRepository lookupCategoryRepository)
        : base(lookupCategoryRepository)
    {
    }

    public async Task<CallResponse> Handle(CreateLookupCategoryCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        var normalized = LookupCategory.Normalize(name);

        if (await this.IsNameInUseAsync(normalized, excludeId: null, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Lookup.CategoryNameInUse));
        }

        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();

        var category = command.IsGlobal
            ? LookupCategory.NewGlobal(name, description)
            : LookupCategory.New(name, description);

        await this.LookupCategoryRepository.AddAsync(category, cancellationToken);
        await this.LookupCategoryRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Created);
    }
}