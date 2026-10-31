using Harmony.Identity.Domain.Models.SecurityAuditModule;

namespace Harmony.Identity.Handler.Queries.SecurityAuditModule;

/// <summary>How often each action was written inside a window, across every customer: the console's sign-in numbers.</summary>
public class GetPlatformTrailCountsQuery : BaseQueryRequest<CallResponse<List<TrailCountModel>>>
{
    /// <summary>From this instant on (inclusive); null: the last 24 hours.</summary>
    public DateTimeOffset? FromUtc { get; set; }

    /// <summary>Before this instant (exclusive); null: now.</summary>
    public DateTimeOffset? ToUtc { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>Only actions of this family, by prefix ("sign-in."). Null: all.</summary>
    public string? Action { get; set; }
}
