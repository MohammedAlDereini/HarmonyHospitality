using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.CountrySubdivisions.Detail
{
    public class GetCountrySubdivisionByCodeQueryHandler : IRequestHandler<GetCountrySubdivisionByCodeQuery, CallResponse<CountrySubdivisionModel>>
    {
        private readonly ICountrySubdivisionRepository repository;

        public GetCountrySubdivisionByCodeQueryHandler(ICountrySubdivisionRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<CountrySubdivisionModel>> Handle(GetCountrySubdivisionByCodeQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim().ToUpperInvariant();

            var model = await this.repository.Query()
                .Where(e => e.Code == code)
                .Select(ReferenceProjections.CountrySubdivision)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<CountrySubdivisionModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<CountrySubdivisionModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
