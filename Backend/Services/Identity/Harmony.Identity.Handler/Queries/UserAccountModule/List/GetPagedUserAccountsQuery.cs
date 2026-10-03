using Harmony.Identity.Domain.Models.UserAccountModule;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.List
{
    public class GetPagedUserAccountsQuery : BasePaginationQueryRequest<CallResponse<PagedResult<UserAccountModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(UserAccountModel.UserName),
            nameof(UserAccountModel.Email),
            nameof(UserAccountModel.DisplayName),
            nameof(UserAccountModel.State),
            nameof(UserAccountModel.CreatedDate),
            nameof(UserAccountModel.ModifiedDate),
        ];

        public GetPagedUserAccountsQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(UserAccountModel.CreatedDate), SortOrder = eSortOrder.DEC }
            };
        }

        public string? SearchText { get; set; }

        public class GetPagedUserAccountsQueryValidator : AbstractValidator<GetPagedUserAccountsQuery>
        {
            public GetPagedUserAccountsQueryValidator()
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
