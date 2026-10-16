using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Models.TenantModule;

namespace Harmony.Identity.Handler.Queries.TenantModule.List;

public class GetPagedTenantsQuery : BasePaginationQueryRequest<CallResponse<PagedResult<TenantModel>>>
{
    public static readonly string[] SortableMembers =
    [
        nameof(TenantModel.Code),
        nameof(TenantModel.LegalNameEn),
        nameof(TenantModel.LegalNameAr),
        nameof(TenantModel.CountryCode),
        nameof(TenantModel.Status),
        nameof(TenantModel.Tier),
        nameof(TenantModel.OnboardedOn),
        nameof(TenantModel.CreatedDate),
        nameof(TenantModel.ModifiedDate),
    ];

    public GetPagedTenantsQuery()
    {
        this.SortOrder = new List<SortOrderOption>
        {
            new SortOrderOption { MemberName = nameof(TenantModel.Code), SortOrder = eSortOrder.ASC },
        };
    }

    /// <summary>Matches the code, either legal name or the external identifier; at least 2 characters.</summary>
    public string? SearchText { get; set; }

    /// <summary>Only tenants in this status. Null: all.</summary>
    public TenantStatus? Status { get; set; }

    public class GetPagedTenantsQueryValidator : AbstractValidator<GetPagedTenantsQuery>
    {
        public GetPagedTenantsQueryValidator()
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
