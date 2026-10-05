using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Currencies.Detail
{
    public class GetCurrencyByCodeQueryHandler : IRequestHandler<GetCurrencyByCodeQuery, CallResponse<CurrencyModel>>
    {
        private readonly ICurrencyRepository repository;

        public GetCurrencyByCodeQueryHandler(ICurrencyRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<CurrencyModel>> Handle(GetCurrencyByCodeQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim().ToUpperInvariant();

            var model = await this.repository.Query()
                .Where(e => e.Code == code)
                .Select(ReferenceProjections.Currency)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<CurrencyModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<CurrencyModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
