using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;

/// <summary>
/// The named thing that happened (design 4.4, the ERD's SecurityOperations): "account.suspended", "account.roles-changed".
/// The revocation feed and the approvals point at it; the trail (SecurityAuditEvents) tells the story around it. It is
/// only ever written, never changed. Kind stays a free string, like the trail's Action.
/// </summary>
public sealed class SecurityOperation : IMultiTenantEntity
{
    public const int KindMaxLength = 64;

    private SecurityOperation()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Kind { get; private set; } = null!;

    /// <summary>The account it was about.</summary>
    public Guid? UserAccountId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static SecurityOperation Named(Guid tenantId, string kind, Guid? userAccountId, DateTimeOffset at)
    {
        var text = (kind ?? string.Empty).Trim();
        return new SecurityOperation
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Kind = text.Length == 0 ? "security.change" : text.Length <= KindMaxLength ? text : text[..KindMaxLength],
            UserAccountId = userAccountId,
            OccurredAtUtc = at.ToUniversalTime(),
        };
    }
}
