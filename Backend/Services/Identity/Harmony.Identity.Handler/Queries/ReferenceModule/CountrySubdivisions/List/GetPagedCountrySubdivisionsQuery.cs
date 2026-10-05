using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.CountrySubdivisions.List
{
    public class GetPagedCountrySubdivisionsQuery : BasePaginationQueryRequest<CallResponse<PagedResult<CountrySubdivisionModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(CountrySubdivisionModel.Code),
            nameof(CountrySubdivisionModel.CountryCode),
            nameof(CountrySubdivisionModel.NameEn),
            nameof(CountrySubdivisionModel.NameAr),
            nameof(CountrySubdivisionModel.Kind),
            nameof(CountrySubdivisionModel.CreatedDate),
            nameof(CountrySubdivisionModel.ModifiedDate),
        ];

        public GetPagedCountrySubdivisionsQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(CountrySubdivisionModel.Code), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Only this country's subdivisions. Empty: every country.</summary>
        public string? CountryCode { get; set; }

        /// <summary>Matches the code or either name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public class GetPagedCountrySubdivisionsQueryValidator : AbstractValidator<GetPagedCountrySubdivisionsQuery>
        {
            public GetPagedCountrySubdivisionsQueryValidator()
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
            }
        }
    }
}
