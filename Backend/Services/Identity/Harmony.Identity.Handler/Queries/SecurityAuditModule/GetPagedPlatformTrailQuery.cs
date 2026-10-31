using Harmony.Identity.Domain.Models.SecurityAuditModule;

namespace Harmony.Identity.Handler.Queries.SecurityAuditModule;

/// <summary>
/// The platform's security trail across every customer (the console's Audit log and Sign-ins). Newest first unless the
/// caller sorts otherwise; every filter is optional and they stack.
/// </summary>
public class GetPagedPlatformTrailQuery : BasePaginationQueryRequest<CallResponse<PagedResult<PlatformTrailEventModel>>>
{
    public static readonly string[] SortableMembers =
    [
        nameof(PlatformTrailEventModel.OccurredAtUtc),
        nameof(PlatformTrailEventModel.Action),
        nameof(PlatformTrailEventModel.TenantCode),
        nameof(PlatformTrailEventModel.UserDisplayName),
    ];

    public GetPagedPlatformTrailQuery()
    {
        this.SortOrder = new List<SortOrderOption>
        {
            new SortOrderOption { MemberName = nameof(PlatformTrailEventModel.OccurredAtUtc), SortOrder = eSortOrder.DEC },
        };
    }

    /// <summary>Matches the action, the detail, the actor, the address, the device, the customer's code or names, or the person's name; at least 2 characters.</summary>
    public string? SearchText { get; set; }

    /// <summary>One action, or a family of actions by its prefix ("sign-in." is every sign-in). Null: all.</summary>
    public string? Action { get; set; }

    /// <summary>Only these actions, by name (the Sign-ins page's outcome filter). Empty or null: no narrowing.</summary>
    public string[]? Actions { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? UserAccountId { get; set; }

    /// <summary>Only what one actor did: a platform person's account id as text, or "system". Null: everyone.</summary>
    public string? ActorSubject { get; set; }

    /// <summary>From this instant on (inclusive).</summary>
    public DateTimeOffset? FromUtc { get; set; }

    /// <summary>Before this instant (exclusive).</summary>
    public DateTimeOffset? ToUtc { get; set; }

    public class GetPagedPlatformTrailQueryValidator : AbstractValidator<GetPagedPlatformTrailQuery>
    {
        public GetPagedPlatformTrailQueryValidator()
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
