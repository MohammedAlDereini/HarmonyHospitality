using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.RegulatoryEnvironments.List
{
    public class GetPagedRegulatoryEnvironmentsQuery : BasePaginationQueryRequest<CallResponse<PagedResult<RegulatoryEnvironmentModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(RegulatoryEnvironmentModel.Code),
            nameof(RegulatoryEnvironmentModel.CountryCode),
            nameof(RegulatoryEnvironmentModel.Kind),
            nameof(RegulatoryEnvironmentModel.NameEn),
            nameof(RegulatoryEnvironmentModel.NameAr),
            nameof(RegulatoryEnvironmentModel.CreatedDate),
            nameof(RegulatoryEnvironmentModel.ModifiedDate),
        ];

        public GetPagedRegulatoryEnvironmentsQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(RegulatoryEnvironmentModel.Code), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Only this country's rows (ISO 3166-1 alpha-2). Empty: every country.</summary>
        public string? CountryCode { get; set; }

        /// <summary>Only rows of this kind. Null: every kind.</summary>
        public RegulatoryKind? Kind { get; set; }

        /// <summary>Matches the code or either name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public class GetPagedRegulatoryEnvironmentsQueryValidator : AbstractValidator<GetPagedRegulatoryEnvironmentsQuery>
        {
            public GetPagedRegulatoryEnvironmentsQueryValidator()
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

                this.RuleFor(e => e.CountryCode)
                    .Matches(ReferenceRules.CountryCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CountryCodeInvalidFormat)
                    .When(e => !string.IsNullOrWhiteSpace(e.CountryCode));

                this.RuleFor(e => e.Kind)
                    .IsInEnum().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.KindInvalid)
                    .When(e => e.Kind.HasValue);
            }
        }
    }
}
