using Harmony.Identity.Domain.Models.PropertyGroupModule;

namespace Harmony.Identity.Handler.Queries.PropertyGroupModule.Mine;

public class GetMyPropertyGroupByIdQuery : BaseQueryRequest<CallResponse<PropertyGroupModel>>
{
    public Guid Id { get; set; }

    public class GetMyPropertyGroupByIdQueryValidator : AbstractValidator<GetMyPropertyGroupByIdQuery>
    {
        public GetMyPropertyGroupByIdQueryValidator()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.IdRequired);
        }
    }
}
