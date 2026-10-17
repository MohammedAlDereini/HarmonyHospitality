namespace Harmony.Identity.Handler.Commands.ReferenceModule.Modules.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreateModuleCommandHandler : IRequestHandler<CreateModuleCommand, CallResponse<string>>
{
    private readonly IModuleRepository repository;

    public CreateModuleCommandHandler(IModuleRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreateModuleCommand command, CancellationToken cancellationToken)
    {
        var module = Module.Create(command.Code, command.NameEn, command.OwnerService, command.Level, command.HasLimit);

        if (await this.repository.ExistsAsync(module.Code, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        await this.repository.AddAsync(module, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(module.Code);
    }
}
