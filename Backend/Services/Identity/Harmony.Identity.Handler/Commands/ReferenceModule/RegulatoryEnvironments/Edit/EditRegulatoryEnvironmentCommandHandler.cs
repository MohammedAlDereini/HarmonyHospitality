namespace Harmony.Identity.Handler.Commands.ReferenceModule.RegulatoryEnvironments.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditRegulatoryEnvironmentCommandHandler : IRequestHandler<EditRegulatoryEnvironmentCommand, CallResponse>
{
    private readonly IRegulatoryEnvironmentRepository repository;

    public EditRegulatoryEnvironmentCommandHandler(IRegulatoryEnvironmentRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditRegulatoryEnvironmentCommand command, CancellationToken cancellationToken)
    {
        var environment = await this.repository.GetByCodeAsync(command.Code.Trim(), cancellationToken);

        if (environment is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        environment.Update(command.NameEn, command.NameAr, command.OwnerService);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
