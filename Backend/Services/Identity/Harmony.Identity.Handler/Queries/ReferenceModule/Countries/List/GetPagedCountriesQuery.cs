using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Countries.List
{
    public class GetPagedCountriesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<CountryModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(CountryModel.Code),
            nameof(CountryModel.Alpha3),
            nameof(CountryModel.NameEn),
            nameof(CountryModel.NameAr),
            nameof(CountryModel.DefaultCurrency),
            nameof(CountryModel.IsActive),
            nameof(CountryModel.CreatedDate),
            nameof(CountryModel.ModifiedDate),
        ];

        public GetPagedCountriesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(CountryModel.Code), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Matches the code, alpha-3 or either name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        /// <summary>Only active (true) or only inactive (false) rows. Null: all.</summary>
        public bool? IsActive { get; set; }

        public class GetPagedCountriesQueryValidator : AbstractValidator<GetPagedCountriesQuery>
        {
            public GetPagedCountriesQueryValidator()
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
            }
        }
    }
}
