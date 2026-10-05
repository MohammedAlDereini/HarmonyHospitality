using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.PropertyTypes.Detail
{
    public class GetPropertyTypeByCodeQuery : BaseQueryRequest<CallResponse<PropertyTypeModel>>
    {
        public string Code { get; set; } = null!;

        public class GetPropertyTypeByCodeQueryValidator : AbstractValidator<GetPropertyTypeByCodeQuery>
        {
            public GetPropertyTypeByCodeQueryValidator()
            {
                this.RuleFor(e => e.Code)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.CodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);
            }
        }
    }
}
