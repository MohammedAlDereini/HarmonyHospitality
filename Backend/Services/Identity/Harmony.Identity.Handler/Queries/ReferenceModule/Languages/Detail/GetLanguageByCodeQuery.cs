using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Languages.Detail
{
    public class GetLanguageByCodeQuery : BaseQueryRequest<CallResponse<LanguageModel>>
    {
        public string Code { get; set; } = null!;

        public class GetLanguageByCodeQueryValidator : AbstractValidator<GetLanguageByCodeQuery>
        {
            public GetLanguageByCodeQueryValidator()
            {
                this.RuleFor(e => e.Code)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.LanguageCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);
            }
        }
    }
}
