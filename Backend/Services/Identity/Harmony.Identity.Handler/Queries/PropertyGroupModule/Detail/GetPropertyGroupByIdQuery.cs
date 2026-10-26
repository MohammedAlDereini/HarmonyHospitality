using Harmony.Identity.Domain.Models.PropertyGroupModule;

namespace Harmony.Identity.Handler.Queries.PropertyGroupModule.Detail;

public class GetPropertyGroupByIdQuery : BaseQueryRequest<CallResponse<PropertyGroupModel>>
{
    public Guid Id { get; set; }

    public class GetPropertyGroupByIdQueryValidator : AbstractValidator<GetPropertyGroupByIdQuery>
    {
        public GetPropertyGroupByIdQueryValidator()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.IdRequired);
        }
    }
}
