using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Capabilities.Detail
{
    public class GetCapabilityByCodeQuery : BaseQueryRequest<CallResponse<CapabilityModel>>
    {
        public string Code { get; set; } = null!;

        public class GetCapabilityByCodeQueryValidator : AbstractValidator<GetCapabilityByCodeQuery>
        {
            public GetCapabilityByCodeQueryValidator()
            {
                this.RuleFor(e => e.Code)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.CapabilityCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);
            }
        }
    }
}
