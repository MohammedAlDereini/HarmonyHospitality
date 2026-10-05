namespace Harmony.Identity.Handler.Commands.ReferenceModule.Countries.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreateCountryCommandHandler : IRequestHandler<CreateCountryCommand, CallResponse<string>>
{
    private readonly ICountryRepository repository;

    public CreateCountryCommandHandler(ICountryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreateCountryCommand command, CancellationToken cancellationToken)
    {
        // The entity trims and upper-cases the codes, so every check below runs on the stored form.
        var country = Country.Create(command.Code, command.Alpha3, command.NameEn, command.NameAr, command.DialCode, command.DefaultCurrency);

        if (!await this.repository.CurrencyExistsAsync(country.DefaultCurrency, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.UnknownCurrency));
        }

        if (await this.repository.ExistsAsync(country.Code, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        if (await this.repository.Alpha3ExistsAsync(country.Alpha3, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.Alpha3InUse));
        }

        await this.repository.AddAsync(country, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(country.Code);
    }
}
