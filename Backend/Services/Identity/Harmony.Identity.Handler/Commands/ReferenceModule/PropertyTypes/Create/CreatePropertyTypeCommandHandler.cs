namespace Harmony.Identity.Handler.Commands.ReferenceModule.PropertyTypes.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreatePropertyTypeCommandHandler : IRequestHandler<CreatePropertyTypeCommand, CallResponse<string>>
{
    private readonly IPropertyTypeRepository repository;

    public CreatePropertyTypeCommandHandler(IPropertyTypeRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreatePropertyTypeCommand command, CancellationToken cancellationToken)
    {
        var propertyType = PropertyTypeRef.Create(command.Code, command.NameEn, command.NameAr, command.DisplayOrder);

        if (await this.repository.ExistsAsync(propertyType.Code, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        await this.repository.AddAsync(propertyType, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(propertyType.Code);
    }
}
