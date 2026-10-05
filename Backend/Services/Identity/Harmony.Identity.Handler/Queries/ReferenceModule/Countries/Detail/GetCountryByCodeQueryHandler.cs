using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Countries.Detail
{
    public class GetCountryByCodeQueryHandler : IRequestHandler<GetCountryByCodeQuery, CallResponse<CountryModel>>
    {
        private readonly ICountryRepository repository;

        public GetCountryByCodeQueryHandler(ICountryRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<CountryModel>> Handle(GetCountryByCodeQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim().ToUpperInvariant();

            var model = await this.repository.Query()
                .Where(e => e.Code == code)
                .Select(ReferenceProjections.Country)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<CountryModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<CountryModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
