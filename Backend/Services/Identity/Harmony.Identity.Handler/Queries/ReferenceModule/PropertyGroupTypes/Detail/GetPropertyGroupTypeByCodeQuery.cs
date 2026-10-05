using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.PropertyGroupTypes.Detail
{
    public class GetPropertyGroupTypeByCodeQuery : BaseQueryRequest<CallResponse<PropertyGroupTypeModel>>
    {
        public string Code { get; set; } = null!;

        public class GetPropertyGroupTypeByCodeQueryValidator : AbstractValidator<GetPropertyGroupTypeByCodeQuery>
        {
            public GetPropertyGroupTypeByCodeQueryValidator()
            {
                this.RuleFor(e => e.Code)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.CodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);
            }
        }
    }
}
