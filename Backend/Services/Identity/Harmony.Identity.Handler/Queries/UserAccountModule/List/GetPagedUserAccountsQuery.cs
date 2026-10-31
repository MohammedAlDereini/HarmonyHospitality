using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
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

        /// <summary>Only accounts in this state. Null: every state.</summary>
        public AdministrativeState? State { get; set; }

        /// <summary>"on": two-step set up; "off": not set up; "exempt": on the exception list. Null: everyone.</summary>
        public string? TwoStep { get; set; }

        /// <summary>Only accounts holding this role. Null: every role.</summary>
        public Guid? RoleId { get; set; }

        public class GetPagedUserAccountsQueryValidator : AbstractValidator<GetPagedUserAccountsQuery>
        {
            public GetPagedUserAccountsQueryValidator()
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
