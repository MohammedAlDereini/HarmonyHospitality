using Harmony.Identity.Domain.Models.PropertyModule;

namespace Harmony.Identity.Handler.Queries.PropertyModule.Detail;

public class GetPropertyByIdQuery : BaseQueryRequest<CallResponse<PropertyModel>>
{
    public Guid Id { get; set; }

    public class GetPropertyByIdQueryValidator : AbstractValidator<GetPropertyByIdQuery>
    {
        public GetPropertyByIdQueryValidator()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);
        }
    }
}
