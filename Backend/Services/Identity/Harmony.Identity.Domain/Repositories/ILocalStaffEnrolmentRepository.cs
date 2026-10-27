using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.EnrolmentModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Invitations. Reads name the account or the customer: a first password is set before anybody is signed in.</summary>
public interface ILocalStaffEnrolmentRepository : IRepository<LocalStaffEnrolment>
{
    /// <summary>The account's newest invitation, tracked; null when it was never invited (made with a starting password, or before invitations were kept).</summary>
    Task<LocalStaffEnrolment?> LatestOfAccountAsync(Guid userAccountId, CancellationToken cancellationToken);

    /// <summary>The customer's invitations still Pending (open or expired), newest first, untracked.</summary>
    Task<IReadOnlyList<LocalStaffEnrolment>> PendingOfTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}
