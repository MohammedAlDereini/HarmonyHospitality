namespace Harmony.Identity.Handler.Commands.ReferenceModule.Capabilities.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditCapabilityCommandHandler : IRequestHandler<EditCapabilityCommand, CallResponse>
{
    private readonly ICapabilityRepository repository;

    public EditCapabilityCommandHandler(ICapabilityRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditCapabilityCommand command, CancellationToken cancellationToken)
    {
        var capability = await this.repository.GetByCodeAsync(command.Code.Trim(), cancellationToken);

        if (capability is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        capability.Update(command.NameEn, command.OwnerService, command.HasLimit);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
