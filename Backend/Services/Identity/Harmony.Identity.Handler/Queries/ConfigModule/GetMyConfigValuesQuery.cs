using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Handler.Commands.ConfigModule.Values;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

/// <summary>
/// The caller's own customer's values, paged, each with its place named in words. The tenant filter walls the rows. A
/// call that names a hotel sees what reaches that hotel only (its own values, its groups', the customer's): a hotel
/// role's ViewSettings counts only on such a call, and it must not open the whole chain.
/// </summary>
public class GetMyConfigValuesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<ConfigValueModel>>>
{
    public static readonly string[] SortableMembers = [nameof(ConfigValueModel.Key), nameof(ConfigValueModel.Scope), nameof(ConfigValueModel.ModifiedDate)];

    public GetMyConfigValuesQuery()
    {
        this.SortOrder = new List<SortOrderOption>
        {
            new SortOrderOption { MemberName = nameof(ConfigValueModel.Key), SortOrder = eSortOrder.ASC },
        };
    }

    /// <summary>Only this key. Empty: every key.</summary>
    public string? Key { get; set; }

    /// <summary>Only this level: Tenant, PropertyGroup or Property. Empty: every level.</summary>
    public string? Scope { get; set; }

    /// <summary>Only this hotel or group.</summary>
    public Guid? ScopeId { get; set; }

    public class GetMyConfigValuesQueryValidator : AbstractValidator<GetMyConfigValuesQuery>
    {
        public GetMyConfigValuesQueryValidator()
        {
            this.RuleFor(e => e.PageNumber).GreaterThan(0).WithErrorCode(ModelValidationErrorCodes.General.PageNumberOutOfRange);
            this.RuleFor(e => e.PageSize).InclusiveBetween(1, 100).WithErrorCode(ModelValidationErrorCodes.General.PageSizeOutOfRange);
            this.RuleForEach(e => e.SortOrder)
                .Must(s => s is not null && SortableMembers.Contains(s.MemberName, StringComparer.OrdinalIgnoreCase))
                .WithErrorCode(ModelValidationErrorCodes.General.SortMemberInvalid);
            this.RuleFor(e => e.Scope)
                .Must(text => ConfigValueRules.LevelOf(text) is not null)
                .When(e => !string.IsNullOrWhiteSpace(e.Scope))
                .WithErrorCode(ModelValidationErrorCodes.Identity.Config.ScopeInvalid);
        }
    }
}
