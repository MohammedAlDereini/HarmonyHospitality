namespace Harmony.Identity.Handler.Commands.ReferenceModule.Countries.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditCountryCommandHandler : IRequestHandler<EditCountryCommand, CallResponse>
{
    private readonly ICountryRepository repository;

    public EditCountryCommandHandler(ICountryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditCountryCommand command, CancellationToken cancellationToken)
    {
        var country = await this.repository.GetByCodeAsync(command.Code.Trim().ToUpperInvariant(), cancellationToken);

        if (country is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        var currency = command.DefaultCurrency.Trim().ToUpperInvariant();

        if (!await this.repository.CurrencyExistsAsync(currency, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.UnknownCurrency));
        }

        country.Update(command.NameEn, command.NameAr, command.DialCode, currency);
        country.SetActive(command.IsActive);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
