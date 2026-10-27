using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.MergeApprovalModule;

/// <summary>
/// Two accounts that turned out to be one person (design 4.3, the ERD's MergeApprovals). The one kept is the proposed
/// account; the other is superseded and terminated, never deleted: deleting it would orphan every audit row naming it.
/// MakerCheckerRequestId is set when the customer requires approvals and the merge went through one.
/// </summary>
public sealed class MergeApproval : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity
{
    private MergeApproval()
    {
    }

    private MergeApproval(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>The account kept.</summary>
    public Guid ProposedUserAccountId { get; private set; }

    /// <summary>The account merged into it.</summary>
    public Guid SupersededUserAccountId { get; private set; }

    public Guid? MakerCheckerRequestId { get; private set; }

    public DateTimeOffset MergedAtUtc { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public static MergeApproval Record(Guid tenantId, Guid keptId, Guid supersededId, Guid? requestId, DateTimeOffset now)
    {
        if (keptId == supersededId)
        {
            throw new ArgumentException("An account is never merged into itself.");
        }

        return new MergeApproval(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            ProposedUserAccountId = keptId,
            SupersededUserAccountId = supersededId,
            MakerCheckerRequestId = requestId,
            MergedAtUtc = now.ToUniversalTime(),
        };
    }
}
