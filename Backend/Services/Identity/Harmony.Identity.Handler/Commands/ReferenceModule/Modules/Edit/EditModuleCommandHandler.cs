namespace Harmony.Identity.Handler.Commands.ReferenceModule.Modules.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditModuleCommandHandler : IRequestHandler<EditModuleCommand, CallResponse>
{
    private readonly IModuleRepository repository;

    public EditModuleCommandHandler(IModuleRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditModuleCommand command, CancellationToken cancellationToken)
    {
        var module = await this.repository.GetByCodeAsync(command.Code.Trim(), cancellationToken);

        if (module is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        module.Update(command.NameEn, command.OwnerService, command.Level, command.HasLimit);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
