namespace Harmony.Identity.Handler.Commands.ReferenceModule.Currencies.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditCurrencyCommandHandler : IRequestHandler<EditCurrencyCommand, CallResponse>
{
    private readonly ICurrencyRepository repository;

    public EditCurrencyCommandHandler(ICurrencyRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditCurrencyCommand command, CancellationToken cancellationToken)
    {
        var currency = await this.repository.GetByCodeAsync(command.Code.Trim().ToUpperInvariant(), cancellationToken);

        if (currency is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        currency.Update(command.NameEn, command.NameAr);
        currency.SetActive(command.IsActive);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
