using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.PropertyGroupTypes.Detail
{
    public class GetPropertyGroupTypeByCodeQueryHandler : IRequestHandler<GetPropertyGroupTypeByCodeQuery, CallResponse<PropertyGroupTypeModel>>
    {
        private readonly IPropertyGroupTypeRepository repository;

        public GetPropertyGroupTypeByCodeQueryHandler(IPropertyGroupTypeRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PropertyGroupTypeModel>> Handle(GetPropertyGroupTypeByCodeQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim();

            var model = await this.repository.Query()
                .Where(e => e.Code == code)
                .Select(ReferenceProjections.PropertyGroupType)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<PropertyGroupTypeModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<PropertyGroupTypeModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
