using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Reads only. Writes go through ASP.NET Core Identity's UserManager, which validates and saves.</summary>
public interface IUserAccountRepository : IRepository<User>
{
    public IQueryable<User> Query();

    /// <summary>The account with its roles, tracked, for a roles change.</summary>
    public Task<User?> GetWithRolesAsync(Guid id, CancellationToken cancellationToken);

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
}
