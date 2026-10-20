using Harmony.Identity.Domain.Models.PropertyModule;

namespace Harmony.Identity.Handler.Queries.PropertyModule.Mine;

public class GetMyPropertyByIdQuery : BaseQueryRequest<CallResponse<PropertyModel>>
{
    public Guid Id { get; set; }

    public class GetMyPropertyByIdQueryValidator : AbstractValidator<GetMyPropertyByIdQuery>
    {
        public GetMyPropertyByIdQueryValidator()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);
        }
    }
}
