using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Modules.Detail
{
    public class GetModuleByCodeQueryHandler : IRequestHandler<GetModuleByCodeQuery, CallResponse<ModuleModel>>
    {
        private readonly IModuleRepository repository;

        public GetModuleByCodeQueryHandler(IModuleRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<ModuleModel>> Handle(GetModuleByCodeQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim();

            var model = await this.repository.Query()
                .Where(e => e.Code == code)
                .Select(ReferenceProjections.Module)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<ModuleModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<ModuleModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
