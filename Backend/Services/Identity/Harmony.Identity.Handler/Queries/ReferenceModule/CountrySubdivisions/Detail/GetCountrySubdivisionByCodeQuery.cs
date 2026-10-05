using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.CountrySubdivisions.Detail
{
    public class GetCountrySubdivisionByCodeQuery : BaseQueryRequest<CallResponse<CountrySubdivisionModel>>
    {
        public string Code { get; set; } = null!;

        public class GetCountrySubdivisionByCodeQueryValidator : AbstractValidator<GetCountrySubdivisionByCodeQuery>
        {
            public GetCountrySubdivisionByCodeQueryValidator()
            {
                this.RuleFor(e => e.Code)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.SubdivisionCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionCodeInvalidFormat);
            }
        }
    }
}
