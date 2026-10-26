using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Reads only. Writes go through ASP.NET Core Identity's UserManager, which validates and saves.</summary>
public interface IUserAccountRepository : IRepository<User>
{
    public IQueryable<User> Query();

    /// <summary>The account with its roles, tracked, for a roles change.</summary>
    public Task<User?> GetWithRolesAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The account with its roles, assignments and grants, tracked, for a change to any of them.</summary>
    public Task<User?> GetWithAccessAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The role ids that reach this hotel for this account right now: assignments and grants naming the hotel, or a group
    /// the hotel is a current member of. The tenant filter walls the rows.
    /// </summary>
    public Task<IReadOnlyList<Guid>> RoleIdsAtHotelAsync(Guid userId, Guid propertyId, DateTimeOffset utcNow, CancellationToken cancellationToken);

    /// <summary>Does the account hold any tenant-wide role (UserRoles): those act at every hotel of the customer.</summary>
    public Task<bool> HoldsAnyTenantWideRoleAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Every account with this normalised user name in ANY tenant, tracked. For the anonymous doors (forgot / reset password),
    /// where no tenant is known yet; the caller acts only when exactly one comes back.
    /// </summary>
    public Task<IReadOnlyList<User>> FindByUserNameAcrossTenantsAsync(string normalizedUserName, CancellationToken cancellationToken);

    /// <summary>
    /// The account with this id in ANY tenant, tracked. For the sign-in's second step, where only the id from the
    /// two-factor challenge is known and no tenant is chosen yet.
    /// </summary>
    public Task<User?> FindByIdAcrossTenantsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The first account holding a role inside a named tenant, whatever tenant the caller is in (a tenant's administrator).</summary>
    public Task<User?> FindWithRoleInTenantAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the account holds a Manager or higher role. Searches every tenant (the id is unique), because sign-in asks
    /// before any tenant is chosen; such a person always needs two-step, exception list or not.
    /// </summary>
    public Task<bool> HoldsPrivilegedRoleAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The account's rank: the highest privilege level among its roles in the caller's tenant, as a number
    /// (RoleRank: the super role above every level, no roles at all = 0). An unknown id is simply rank 0.
    /// </summary>
    public Task<int> RankOfAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// True when every ACTIVE administrator of the tenant stands on this one role: demoting or deleting it
    /// would leave the tenant with nobody in charge.
    /// </summary>
    public Task<bool> SoleAdministratorRoleAsync(Guid roleId, CancellationToken cancellationToken);
}
