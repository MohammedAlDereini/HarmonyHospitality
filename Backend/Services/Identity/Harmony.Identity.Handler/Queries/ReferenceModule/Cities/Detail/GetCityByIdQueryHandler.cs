using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Cities.Detail
{
    public class GetCityByIdQueryHandler : IRequestHandler<GetCityByIdQuery, CallResponse<CityModel>>
    {
        private readonly ICityRepository repository;

        public GetCityByIdQueryHandler(ICityRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<CityModel>> Handle(GetCityByIdQuery request, CancellationToken cancellationToken)
        {
            var model = await this.repository.Query()
                .Where(e => e.Id == request.Id)
                .Select(ReferenceProjections.City)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<CityModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<CityModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
