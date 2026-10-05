using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Countries.Detail
{
    public class GetCountryByCodeQuery : BaseQueryRequest<CallResponse<CountryModel>>
    {
        public string Code { get; set; } = null!;

        public class GetCountryByCodeQueryValidator : AbstractValidator<GetCountryByCodeQuery>
        {
            public GetCountryByCodeQueryValidator()
            {
                this.RuleFor(e => e.Code)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.CountryCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);
            }
        }
    }
}
