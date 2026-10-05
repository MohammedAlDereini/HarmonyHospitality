using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.TimeZones.Detail
{
    public class GetTimeZoneByIdQuery : BaseQueryRequest<CallResponse<TimeZoneModel>>
    {
        /// <summary>IANA id, like Asia/Amman.</summary>
        public string Id { get; set; } = null!;

        public class GetTimeZoneByIdQueryValidator : AbstractValidator<GetTimeZoneByIdQuery>
        {
            public GetTimeZoneByIdQueryValidator()
            {
                this.RuleFor(e => e.Id)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.TimeZoneIdRequired)
                    .Matches(ReferenceRules.TimeZoneIdPattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.TimeZoneIdInvalidFormat);
            }
        }
    }
}
