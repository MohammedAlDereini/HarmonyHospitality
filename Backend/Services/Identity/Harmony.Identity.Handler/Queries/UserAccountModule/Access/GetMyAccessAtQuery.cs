using Harmony.Core.Identity.Permissions;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Access;

/// <summary>The caller at one hotel: may they act there, and with which roles beyond the token's. What a service asks when it holds no copy.</summary>
public class GetMyAccessAtQuery : BaseQueryRequest<CallResponse<HotelAccess>>
{
    public Guid PropertyId { get; set; }

    public class GetMyAccessAtQueryValidator : AbstractValidator<GetMyAccessAtQuery>
    {
        public GetMyAccessAtQueryValidator()
        {
            this.RuleFor(e => e.PropertyId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.PropertyRequired);
        }
    }
}
