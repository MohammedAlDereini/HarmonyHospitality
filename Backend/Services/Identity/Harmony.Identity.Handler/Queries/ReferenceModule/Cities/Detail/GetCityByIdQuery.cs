using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Cities.Detail
{
    public class GetCityByIdQuery : BaseQueryRequest<CallResponse<CityModel>>
    {
        public Guid Id { get; set; }

        public class GetCityByIdQueryValidator : AbstractValidator<GetCityByIdQuery>
        {
            public GetCityByIdQueryValidator()
            {
                this.RuleFor(e => e.Id)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.IdRequired);
            }
        }
    }
}
