namespace Harmony.Identity.Handler.Commands.LookupModule.Values.Edit;

using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Repositories;

public class EditLookupValueCommandHandler : LookupValueCommandHandlerBase, IRequestHandler<EditLookupValueCommand, CallResponse>
{
    public EditLookupValueCommandHandler(ILookupValueRepository lookupValueRepository)
        : base(lookupValueRepository)
    {
    }

    public async Task<CallResponse> Handle(EditLookupValueCommand command, CancellationToken cancellationToken)
    {
        var value = await this.LookupValueRepository.GetByIdAsync(command.Id, cancellationToken);

        if (value is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Lookup.ValueNotFound));
        }

        var name = command.Name.Trim();
        var normalized = LookupCategory.Normalize(name);

        if (await this.IsNameInUseInCategoryAsync(value.LookupCategoryId, normalized, excludeId: value.Id, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Lookup.ValueNameInUse));
        }

        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();

        value.Update(name, description, command.IsActive);

        await this.LookupValueRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}