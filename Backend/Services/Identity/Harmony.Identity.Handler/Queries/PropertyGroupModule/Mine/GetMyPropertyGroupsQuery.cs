using Harmony.Identity.Domain.Models.PropertyGroupModule;
using Harmony.Identity.Handler.Queries.PropertyGroupModule.List;

namespace Harmony.Identity.Handler.Queries.PropertyGroupModule.Mine;

/// <summary>The caller's own groups, paged. The tenant filter walls the rows.</summary>
public class GetMyPropertyGroupsQuery : BasePaginationQueryRequest<CallResponse<PagedResult<PropertyGroupModel>>>
{
    public GetMyPropertyGroupsQuery()
    {
        this.SortOrder = new List<SortOrderOption>
        {
            new SortOrderOption { MemberName = nameof(PropertyGroupModel.Code), SortOrder = eSortOrder.ASC },
        };
    }

    /// <summary>Only groups of this type (Brand, Region...). Empty: all.</summary>
    public string? TypeCode { get; set; }

    /// <summary>Matches the code, either name or the external identifier; at least 2 characters.</summary>
    public string? SearchText { get; set; }

    /// <summary>Only active (or only inactive) groups. Null: all.</summary>
    public bool? IsActive { get; set; }

    public class GetMyPropertyGroupsQueryValidator : AbstractValidator<GetMyPropertyGroupsQuery>
    {
        public GetMyPropertyGroupsQueryValidator()
        {
            this.RuleFor(e => e.PageNumber)
                .GreaterThan(0)
                .WithErrorCode(ModelValidationErrorCodes.General.PageNumberOutOfRange);

            this.RuleFor(e => e.PageSize)
                .InclusiveBetween(1, 100)
                .WithErrorCode(ModelValidationErrorCodes.General.PageSizeOutOfRange);

            this.RuleForEach(e => e.SortOrder)
                .Must(s => s is not null && GetPagedPropertyGroupsQuery.SortableMembers.Contains(s.MemberName, StringComparer.OrdinalIgnoreCase))
                .WithErrorCode(ModelValidationErrorCodes.General.SortMemberInvalid);
        }
    }
}
