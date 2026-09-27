using Harmony.Identity.Domain.Models.LookupModule;

namespace Harmony.Identity.Handler.Queries.LookupModule.LookupCategories.List
{
    public class GetPagedLookupCategoriesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<LookupCategoryListModel>>>
    {
        public GetPagedLookupCategoriesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = "CreatedDate", SortOrder = eSortOrder.DEC }
            };
        }

        public string? SearchText { get; set; }

        public class GetPagedLookupCategoriesQueryValidator : AbstractValidator<GetPagedLookupCategoriesQuery>
        {
            public GetPagedLookupCategoriesQueryValidator()
            {
                this.RuleFor(e => e.PageNumber)
                    .GreaterThan(0)
                    .WithMessage("Page Number must be Greater Than 0.");

                this.RuleFor(e => e.PageSize)
                    .GreaterThan(0)
                    .WithMessage("Page Size must be Greater Than 0.");
            }
        }
    }
}