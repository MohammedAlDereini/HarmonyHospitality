using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.IdentityLinkModule;

/// <summary>
/// One stretch of time a company identity was bound to an account (the ERD's ExternalIdentityLinkPeriods). Open while
/// ToUtc is null; one open period per link at a time, held by the filtered unique index. Kept after it closes, so a
/// sign-in from last week can be judged against the link as it was then.
/// </summary>
public sealed class IdentityLinkPeriod : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private IdentityLinkPeriod()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid LinkId { get; private set; }

    public Guid UserAccountId { get; private set; }

    public DateTimeOffset FromUtc { get; private set; }

    /// <summary>Null means current.</summary>
    public DateTimeOffset? ToUtc { get; private set; }

    public bool IsOpen => ToUtc is null;

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    internal static IdentityLinkPeriod Open(Guid tenantId, Guid linkId, Guid userAccountId, DateTimeOffset at)
    {
        // The id is EF's, assigned when the save tracks the row.
        return new IdentityLinkPeriod { TenantId = tenantId, LinkId = linkId, UserAccountId = userAccountId, FromUtc = at.ToUniversalTime() };
    }

    internal void Close(DateTimeOffset at)
    {
        var end = at.ToUniversalTime();
        ToUtc = end < FromUtc ? FromUtc : end;
    }
}
