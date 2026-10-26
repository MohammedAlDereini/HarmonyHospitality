using Harmony.Identity.Domain.Models.PropertyGroupModule;

namespace Harmony.Identity.Handler.Queries.PropertyGroupModule.List;

public class GetPagedPropertyGroupsQuery : BasePaginationQueryRequest<CallResponse<PagedResult<PropertyGroupModel>>>
{
    public static readonly string[] SortableMembers =
    [
        nameof(PropertyGroupModel.Code),
        nameof(PropertyGroupModel.NameEn),
        nameof(PropertyGroupModel.NameAr),
        nameof(PropertyGroupModel.TypeCode),
        nameof(PropertyGroupModel.Precedence),
        nameof(PropertyGroupModel.DisplayOrder),
        nameof(PropertyGroupModel.IsActive),
        nameof(PropertyGroupModel.CreatedDate),
        nameof(PropertyGroupModel.ModifiedDate),
    ];

    public GetPagedPropertyGroupsQuery()
    {
        this.SortOrder = new List<SortOrderOption>
        {
            new SortOrderOption { MemberName = nameof(PropertyGroupModel.Code), SortOrder = eSortOrder.ASC },
        };
    }

    /// <summary>Only this customer's groups. Empty: every customer's.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Only groups of this type (Brand, Region...). Empty: all.</summary>
    public string? TypeCode { get; set; }

    /// <summary>Matches the code, either name or the external identifier; at least 2 characters.</summary>
    public string? SearchText { get; set; }

    /// <summary>Only active (or only inactive) groups. Null: all.</summary>
    public bool? IsActive { get; set; }

    public class GetPagedPropertyGroupsQueryValidator : AbstractValidator<GetPagedPropertyGroupsQuery>
    {
        public GetPagedPropertyGroupsQueryValidator()
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
