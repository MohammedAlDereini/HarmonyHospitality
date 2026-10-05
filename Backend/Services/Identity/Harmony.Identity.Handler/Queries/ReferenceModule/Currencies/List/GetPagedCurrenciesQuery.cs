using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Currencies.List
{
    public class GetPagedCurrenciesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<CurrencyModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(CurrencyModel.Code),
            nameof(CurrencyModel.NameEn),
            nameof(CurrencyModel.NameAr),
            nameof(CurrencyModel.MinorUnits),
            nameof(CurrencyModel.IsActive),
            nameof(CurrencyModel.CreatedDate),
            nameof(CurrencyModel.ModifiedDate),
        ];

        public GetPagedCurrenciesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(CurrencyModel.Code), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Matches the code or either name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public bool? IsActive { get; set; }

        public class GetPagedCurrenciesQueryValidator : AbstractValidator<GetPagedCurrenciesQuery>
        {
            public GetPagedCurrenciesQueryValidator()
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
