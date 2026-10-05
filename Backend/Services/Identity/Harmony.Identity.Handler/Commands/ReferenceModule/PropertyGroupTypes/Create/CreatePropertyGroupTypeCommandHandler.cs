namespace Harmony.Identity.Handler.Commands.ReferenceModule.PropertyGroupTypes.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreatePropertyGroupTypeCommandHandler : IRequestHandler<CreatePropertyGroupTypeCommand, CallResponse<string>>
{
    private readonly IPropertyGroupTypeRepository repository;

    public CreatePropertyGroupTypeCommandHandler(IPropertyGroupTypeRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreatePropertyGroupTypeCommand command, CancellationToken cancellationToken)
    {
        var groupType = PropertyGroupTypeRef.Create(command.Code, command.NameEn, command.NameAr, command.IsExclusive, command.IsSystem);

        if (await this.repository.ExistsAsync(groupType.Code, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        await this.repository.AddAsync(groupType, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(groupType.Code);
    }
}
