namespace Harmony.Identity.Handler.Commands.ReferenceModule.Currencies.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreateCurrencyCommandHandler : IRequestHandler<CreateCurrencyCommand, CallResponse<string>>
{
    private readonly ICurrencyRepository repository;

    public CreateCurrencyCommandHandler(ICurrencyRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreateCurrencyCommand command, CancellationToken cancellationToken)
    {
        // Money rounds by the registry's minor units; a currency the registry does not know cannot be priced.
        if (!CurrencyRef.IsInRegistry(command.Code))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CurrencyNotInRegistry));
        }

        var currency = CurrencyRef.Create(command.Code, command.NameEn, command.NameAr);

        if (await this.repository.ExistsAsync(currency.Code, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        await this.repository.AddAsync(currency, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(currency.Code);
    }
}
