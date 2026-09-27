using Harmony.Identity.Domain.Models.RoleModule;

namespace Harmony.Identity.Handler.Queries.RoleModule.Detail
{
    public class GetRoleByIdQuery : BaseQueryRequest<CallResponse<RoleDetailModel>>
    {
        public Guid Id { get; set; }

        public class GetRoleByIdQueryValidator : AbstractValidator<GetRoleByIdQuery>
        {
            public GetRoleByIdQueryValidator()
            {
                this.RuleFor(e => e.Id)
                    .NotEmpty().WithMessage("This field is required.");
            }
        }
    }
}