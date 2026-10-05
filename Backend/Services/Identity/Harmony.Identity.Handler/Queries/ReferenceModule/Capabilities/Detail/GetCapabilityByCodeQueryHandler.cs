using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Capabilities.Detail
{
    public class GetCapabilityByCodeQueryHandler : IRequestHandler<GetCapabilityByCodeQuery, CallResponse<CapabilityModel>>
    {
        private readonly ICapabilityRepository repository;

        public GetCapabilityByCodeQueryHandler(ICapabilityRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<CapabilityModel>> Handle(GetCapabilityByCodeQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim();

            var model = await this.repository.Query()
                .Where(e => e.Code == code)
                .Select(ReferenceProjections.Capability)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<CapabilityModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<CapabilityModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
