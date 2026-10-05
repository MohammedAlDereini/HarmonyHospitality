using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.RegulatoryEnvironments.Detail
{
    public class GetRegulatoryEnvironmentByCodeQueryHandler : IRequestHandler<GetRegulatoryEnvironmentByCodeQuery, CallResponse<RegulatoryEnvironmentModel>>
    {
        private readonly IRegulatoryEnvironmentRepository repository;

        public GetRegulatoryEnvironmentByCodeQueryHandler(IRegulatoryEnvironmentRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<RegulatoryEnvironmentModel>> Handle(GetRegulatoryEnvironmentByCodeQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim();

            var model = await this.repository.Query()
                .Where(e => e.Code == code)
                .Select(ReferenceProjections.RegulatoryEnvironment)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<RegulatoryEnvironmentModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<RegulatoryEnvironmentModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
