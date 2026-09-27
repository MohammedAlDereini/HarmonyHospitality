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
                    .WithMessage("This field is required.");

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