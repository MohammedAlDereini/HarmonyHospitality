namespace Harmony.Identity.Handler.Commands.ReferenceModule.PropertyGroupTypes.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditPropertyGroupTypeCommandHandler : IRequestHandler<EditPropertyGroupTypeCommand, CallResponse>
{
    private readonly IPropertyGroupTypeRepository repository;

    public EditPropertyGroupTypeCommandHandler(IPropertyGroupTypeRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditPropertyGroupTypeCommand command, CancellationToken cancellationToken)
    {
        var groupType = await this.repository.GetByCodeAsync(command.Code.Trim(), cancellationToken);

        if (groupType is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        // Brand is exclusive because the one-brand-per-hotel rule reads it; a system row's exclusivity is not up for editing.
        if (groupType.IsSystem && groupType.IsExclusive != command.IsExclusive)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.SystemRowProtected));
        }

        groupType.Update(command.NameEn, command.NameAr, command.IsExclusive);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
