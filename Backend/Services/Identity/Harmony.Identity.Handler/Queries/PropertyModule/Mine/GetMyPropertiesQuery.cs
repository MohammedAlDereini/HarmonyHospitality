using Harmony.Identity.Domain.Models.PropertyModule;
using Harmony.Identity.Handler.Queries.PropertyModule.List;

namespace Harmony.Identity.Handler.Queries.PropertyModule.Mine;

/// <summary>The caller's own hotels, paged; the rows only, no lines. The tenant filter walls the rows.</summary>
public class GetMyPropertiesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<PropertyModel>>>
{
    public GetMyPropertiesQuery()
    {
        this.SortOrder = new List<SortOrderOption>
        {
            new SortOrderOption { MemberName = nameof(PropertyModel.Code), SortOrder = eSortOrder.ASC },
        };
    }

    /// <summary>Matches the code, either name or the external identifier; at least 2 characters.</summary>
    public string? SearchText { get; set; }

    /// <summary>Only active (or only inactive) hotels. Null: all.</summary>
    public bool? IsActive { get; set; }

    public class GetMyPropertiesQueryValidator : AbstractValidator<GetMyPropertiesQuery>
    {
        public GetMyPropertiesQueryValidator()
        {
            this.RuleFor(e => e.PageNumber)
                .GreaterThan(0)
                .WithErrorCode(ModelValidationErrorCodes.General.PageNumberOutOfRange);

            this.RuleFor(e => e.PageSize)
                .InclusiveBetween(1, 100)
                .WithErrorCode(ModelValidationErrorCodes.General.PageSizeOutOfRange);

            this.RuleForEach(e => e.SortOrder)
                .Must(s => s is not null && GetPagedPropertiesQuery.SortableMembers.Contains(s.MemberName, StringComparer.OrdinalIgnoreCase))
                .WithErrorCode(ModelValidationErrorCodes.General.SortMemberInvalid);
        }
    }
}
