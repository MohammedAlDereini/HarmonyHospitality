namespace Harmony.Identity.Handler.Commands.ReferenceModule.Capabilities.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreateCapabilityCommandHandler : IRequestHandler<CreateCapabilityCommand, CallResponse<string>>
{
    private readonly ICapabilityRepository repository;

    public CreateCapabilityCommandHandler(ICapabilityRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreateCapabilityCommand command, CancellationToken cancellationToken)
    {
        var capability = Capability.Create(command.Code, command.NameEn, command.OwnerService, command.HasLimit);

        if (await this.repository.ExistsAsync(capability.Code, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        await this.repository.AddAsync(capability, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(capability.Code);
    }
}
