namespace Harmony.Identity.Handler.Commands.ReferenceModule.Languages.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreateLanguageCommandHandler : IRequestHandler<CreateLanguageCommand, CallResponse<string>>
{
    private readonly ILanguageRepository repository;

    public CreateLanguageCommandHandler(ILanguageRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreateLanguageCommand command, CancellationToken cancellationToken)
    {
        var language = Language.Create(command.Code, command.NameEn, command.NameNative, command.IsRightToLeft);

        if (await this.repository.ExistsAsync(language.Code, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        await this.repository.AddAsync(language, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(language.Code);
    }
}
