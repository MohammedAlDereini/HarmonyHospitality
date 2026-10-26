using Harmony.Identity.Domain.Models.UserAccountModule;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Access;

public class GetAccountAccessQuery : BaseQueryRequest<CallResponse<AccountAccessModel>>
{
    public Guid Id { get; set; }

    public class GetAccountAccessQueryValidator : AbstractValidator<GetAccountAccessQuery>
    {
        public GetAccountAccessQueryValidator()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
        }
    }
}
