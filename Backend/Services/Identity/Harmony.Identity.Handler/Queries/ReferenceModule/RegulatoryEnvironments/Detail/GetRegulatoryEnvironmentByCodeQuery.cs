using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.RegulatoryEnvironments.Detail
{
    public class GetRegulatoryEnvironmentByCodeQuery : BaseQueryRequest<CallResponse<RegulatoryEnvironmentModel>>
    {
        public string Code { get; set; } = null!;

        public class GetRegulatoryEnvironmentByCodeQueryValidator : AbstractValidator<GetRegulatoryEnvironmentByCodeQuery>
        {
            public GetRegulatoryEnvironmentByCodeQueryValidator()
            {
                this.RuleFor(e => e.Code)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.CodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);
            }
        }
    }
}
