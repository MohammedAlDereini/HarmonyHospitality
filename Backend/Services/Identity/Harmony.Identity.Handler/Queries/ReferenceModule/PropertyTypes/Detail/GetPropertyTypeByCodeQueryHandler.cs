using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.PropertyTypes.Detail
{
    public class GetPropertyTypeByCodeQueryHandler : IRequestHandler<GetPropertyTypeByCodeQuery, CallResponse<PropertyTypeModel>>
    {
        private readonly IPropertyTypeRepository repository;

        public GetPropertyTypeByCodeQueryHandler(IPropertyTypeRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PropertyTypeModel>> Handle(GetPropertyTypeByCodeQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim();

            var model = await this.repository.Query()
                .Where(e => e.Code == code)
                .Select(ReferenceProjections.PropertyType)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<PropertyTypeModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<PropertyTypeModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
