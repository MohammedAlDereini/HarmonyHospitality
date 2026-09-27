using Harmony.Identity.Domain.Models.RoleModule;

namespace Harmony.Identity.Handler.Queries.RoleModule.List
{
    public class GetPagedRolesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<RoleListModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(RoleListModel.Code),
            nameof(RoleListModel.NameEn),
            nameof(RoleListModel.NameAr),
            nameof(RoleListModel.CreatedDate),
            nameof(RoleListModel.ModifiedDate),
        ];

        public GetPagedRolesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(RoleListModel.CreatedDate), SortOrder = eSortOrder.DEC }
            };
        }

        public string? SearchText { get; set; }

        public class GetPagedRolesQueryValidator : AbstractValidator<GetPagedRolesQuery>
        {
            public GetPagedRolesQueryValidator()
            {
                this.RuleFor(e => e.PageNumber)
                    .GreaterThan(0)
                    .WithMessage("Page Number must be Greater Than 0.");

                this.RuleFor(e => e.PageSize)
                    .InclusiveBetween(1, 100)
                    .WithMessage("Page Size must be between 1 and 100.");

                this.RuleForEach(e => e.SortOrder)
                    .Must(s => s is not null && SortableMembers.Contains(s.MemberName, StringComparer.OrdinalIgnoreCase))
                    .WithMessage($"Sort by one of: {string.Join(", ", SortableMembers)}.");
            }
        }
    }
}