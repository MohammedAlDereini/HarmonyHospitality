using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.TimeZones.Detail
{
    public class GetTimeZoneByIdQueryHandler : IRequestHandler<GetTimeZoneByIdQuery, CallResponse<TimeZoneModel>>
    {
        private readonly ITimeZoneRepository repository;

        public GetTimeZoneByIdQueryHandler(ITimeZoneRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<TimeZoneModel>> Handle(GetTimeZoneByIdQuery request, CancellationToken cancellationToken)
        {
            var id = request.Id.Trim();

            var model = await this.repository.Query()
                .Where(e => e.Id == id)
                .Select(ReferenceProjections.TimeZone)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<TimeZoneModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<TimeZoneModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
