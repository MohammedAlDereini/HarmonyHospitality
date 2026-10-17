using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Modules.Detail
{
    public class GetModuleByCodeQuery : BaseQueryRequest<CallResponse<ModuleModel>>
    {
        public string Code { get; set; } = null!;

        public class GetModuleByCodeQueryValidator : AbstractValidator<GetModuleByCodeQuery>
        {
            public GetModuleByCodeQueryValidator()
            {
                this.RuleFor(e => e.Code)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.ModuleCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);
            }
        }
    }
}
