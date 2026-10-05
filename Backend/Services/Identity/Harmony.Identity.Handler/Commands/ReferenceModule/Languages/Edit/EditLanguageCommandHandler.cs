namespace Harmony.Identity.Handler.Commands.ReferenceModule.Languages.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditLanguageCommandHandler : IRequestHandler<EditLanguageCommand, CallResponse>
{
    private readonly ILanguageRepository repository;

    public EditLanguageCommandHandler(ILanguageRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditLanguageCommand command, CancellationToken cancellationToken)
    {
        var language = await this.repository.GetByCodeAsync(command.Code.Trim().ToLowerInvariant(), cancellationToken);

        if (language is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        language.Update(command.NameEn, command.NameNative, command.IsRightToLeft);
        language.SetActive(command.IsActive);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
