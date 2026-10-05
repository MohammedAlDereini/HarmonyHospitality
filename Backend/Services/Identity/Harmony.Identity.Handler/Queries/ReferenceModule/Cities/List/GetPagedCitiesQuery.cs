using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Cities.List
{
    public class GetPagedCitiesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<CityModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(CityModel.NameEn),
            nameof(CityModel.NameAr),
            nameof(CityModel.CountryCode),
            nameof(CityModel.SubdivisionCode),
            nameof(CityModel.TimeZoneId),
            nameof(CityModel.IsActive),
            nameof(CityModel.CreatedDate),
            nameof(CityModel.ModifiedDate),
        ];

        public GetPagedCitiesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(CityModel.NameEn), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Only this country's cities. Empty: every country.</summary>
        public string? CountryCode { get; set; }

        /// <summary>Only this subdivision's cities, like JO-AM. Empty: every subdivision.</summary>
        public string? SubdivisionCode { get; set; }

        /// <summary>Matches either name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public bool? IsActive { get; set; }

        public class GetPagedCitiesQueryValidator : AbstractValidator<GetPagedCitiesQuery>
        {
            public GetPagedCitiesQueryValidator()
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

                this.RuleFor(e => e.SubdivisionCode)
                    .Matches(ReferenceRules.SubdivisionCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionCodeInvalidFormat)
                    .When(e => !string.IsNullOrWhiteSpace(e.SubdivisionCode));
            }
        }
    }
}
