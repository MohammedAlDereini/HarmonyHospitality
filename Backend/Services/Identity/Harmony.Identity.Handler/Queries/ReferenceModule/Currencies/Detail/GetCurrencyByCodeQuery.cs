using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Currencies.Detail
{
    public class GetCurrencyByCodeQuery : BaseQueryRequest<CallResponse<CurrencyModel>>
    {
        public string Code { get; set; } = null!;

        public class GetCurrencyByCodeQueryValidator : AbstractValidator<GetCurrencyByCodeQuery>
        {
            public GetCurrencyByCodeQueryValidator()
            {
                this.RuleFor(e => e.Code)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.CurrencyCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);
            }
        }
    }
}
