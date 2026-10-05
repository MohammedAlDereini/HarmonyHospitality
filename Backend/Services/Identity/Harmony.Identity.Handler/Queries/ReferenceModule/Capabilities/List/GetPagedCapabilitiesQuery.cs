using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Capabilities.List
{
    public class GetPagedCapabilitiesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<CapabilityModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(CapabilityModel.Code),
            nameof(CapabilityModel.NameEn),
            nameof(CapabilityModel.OwnerService),
            nameof(CapabilityModel.HasLimit),
            nameof(CapabilityModel.CreatedDate),
            nameof(CapabilityModel.ModifiedDate),
        ];

        public GetPagedCapabilitiesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(CapabilityModel.Code), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Only capabilities of this owner service. Empty: all.</summary>
        public string? OwnerService { get; set; }

        /// <summary>Matches the code or the name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public class GetPagedCapabilitiesQueryValidator : AbstractValidator<GetPagedCapabilitiesQuery>
        {
            public GetPagedCapabilitiesQueryValidator()
            {
                this.RuleFor(e => e.PageNumber)
                    .GreaterThan(0)
                    .WithErrorCode(ModelValidationErrorCodes.General.PageNumberOutOfRange);

                this.RuleFor(e => e.PageSize)
                    .InclusiveBetween(1, 100)
                    .WithErrorCode(ModelValidationErrorCodes.General.PageSizeOutOfRange);

                this.RuleForEach(e => e.SortOrder)
                    .Must(s => s is not null && SortableMembers.Contains(s.MemberName, StringComparer.OrdinalIgnoreCase))
                    .WithErrorCode(ModelValidationErrorCodes.General.SortMemberInvalid);

                this.RuleFor(e => e.OwnerService)
                    .MaximumLength(64).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceTooLong)
                    .When(e => !string.IsNullOrWhiteSpace(e.OwnerService));
            }
        }
    }
}
