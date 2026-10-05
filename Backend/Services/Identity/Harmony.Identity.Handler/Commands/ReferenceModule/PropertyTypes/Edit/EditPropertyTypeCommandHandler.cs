namespace Harmony.Identity.Handler.Commands.ReferenceModule.PropertyTypes.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditPropertyTypeCommandHandler : IRequestHandler<EditPropertyTypeCommand, CallResponse>
{
    private readonly IPropertyTypeRepository repository;

    public EditPropertyTypeCommandHandler(IPropertyTypeRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditPropertyTypeCommand command, CancellationToken cancellationToken)
    {
        var propertyType = await this.repository.GetByCodeAsync(command.Code.Trim(), cancellationToken);

        if (propertyType is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        propertyType.Update(command.NameEn, command.NameAr, command.DisplayOrder);
        propertyType.SetActive(command.IsActive);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
