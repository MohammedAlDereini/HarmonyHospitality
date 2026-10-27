using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

/// <summary>The registry, paged: every service's keys, for the console and for the customers who set them.</summary>
public class GetPagedConfigSettingsQuery : BasePaginationQueryRequest<CallResponse<PagedResult<ConfigSettingModel>>>
{
    public static readonly string[] SortableMembers = [nameof(ConfigSettingModel.Key), nameof(ConfigSettingModel.OwnerService), nameof(ConfigSettingModel.DataType)];

    public GetPagedConfigSettingsQuery()
    {
        this.SortOrder = new List<SortOrderOption>
        {
            new SortOrderOption { MemberName = nameof(ConfigSettingModel.Key), SortOrder = eSortOrder.ASC },
        };
    }

    /// <summary>Only the keys of this service (FrontOffice...). Empty: all.</summary>
    public string? OwnerService { get; set; }

    /// <summary>Matches the key or the owner; at least 2 characters.</summary>
    public string? SearchText { get; set; }

    /// <summary>Only switched-on (or only switched-off) keys. Null: all.</summary>
    public bool? IsActive { get; set; }

    public class GetPagedConfigSettingsQueryValidator : AbstractValidator<GetPagedConfigSettingsQuery>
    {
        public GetPagedConfigSettingsQueryValidator()
        {
            this.RuleFor(e => e.PageNumber).GreaterThan(0).WithErrorCode(ModelValidationErrorCodes.General.PageNumberOutOfRange);
            this.RuleFor(e => e.PageSize).InclusiveBetween(1, 100).WithErrorCode(ModelValidationErrorCodes.General.PageSizeOutOfRange);
            this.RuleForEach(e => e.SortOrder)
                .Must(s => s is not null && SortableMembers.Contains(s.MemberName, StringComparer.OrdinalIgnoreCase))
                .WithErrorCode(ModelValidationErrorCodes.General.SortMemberInvalid);
        }
    }
}
