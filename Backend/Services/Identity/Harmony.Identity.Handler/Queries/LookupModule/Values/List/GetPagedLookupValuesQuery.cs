using Harmony.Identity.Domain.Models.LookupModule;

namespace Harmony.Identity.Handler.Queries.LookupModule.Values.List
{
    public class GetPagedLookupValuesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<LookupValueListModel>>>
    {
        public GetPagedLookupValuesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = "CreatedDate", SortOrder = eSortOrder.DEC }
            };
        }

        public Guid LookupCategoryId { get; set; }

        public string? SearchText { get; set; }

        public bool? IsActive { get; set; }

        public class GetPagedLookupValuesQueryValidator : AbstractValidator<GetPagedLookupValuesQuery>
        {
            public GetPagedLookupValuesQueryValidator()
            {
                this.RuleFor(e => e.LookupCategoryId)
                    .NotEmpty()
                    .WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueCategoryIdRequired);

                this.RuleFor(e => e.PageNumber)
                    .GreaterThan(0)
                    .WithErrorCode(ModelValidationErrorCodes.General.PageNumberOutOfRange);

                this.RuleFor(e => e.PageSize)
                    .GreaterThan(0)
                    .WithErrorCode(ModelValidationErrorCodes.General.PageSizeOutOfRange);
            }
        }
    }
}