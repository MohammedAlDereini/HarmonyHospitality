using Harmony.Identity.Domain.Models.PropertyModule;

namespace Harmony.Identity.Handler.Queries.PropertyModule.List;

public class GetPagedPropertiesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<PropertyModel>>>
{
    public static readonly string[] SortableMembers =
    [
        nameof(PropertyModel.Code),
        nameof(PropertyModel.NameEn),
        nameof(PropertyModel.NameAr),
        nameof(PropertyModel.CountryCode),
        nameof(PropertyModel.TotalRooms),
        nameof(PropertyModel.OpensOn),
        nameof(PropertyModel.IsActive),
        nameof(PropertyModel.CreatedDate),
        nameof(PropertyModel.ModifiedDate),
    ];

    public GetPagedPropertiesQuery()
    {
        this.SortOrder = new List<SortOrderOption>
        {
            new SortOrderOption { MemberName = nameof(PropertyModel.Code), SortOrder = eSortOrder.ASC },
        };
    }

    /// <summary>Only this customer's hotels. Empty: every customer's.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Matches the code, either name or the external identifier; at least 2 characters.</summary>
    public string? SearchText { get; set; }

    /// <summary>Only active (or only inactive) hotels. Null: all.</summary>
    public bool? IsActive { get; set; }

    public class GetPagedPropertiesQueryValidator : AbstractValidator<GetPagedPropertiesQuery>
    {
        public GetPagedPropertiesQueryValidator()
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
