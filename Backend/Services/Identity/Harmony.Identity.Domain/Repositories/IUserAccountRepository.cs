using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Reads only. Writes go through ASP.NET Core Identity's UserManager, which validates and saves.</summary>
public interface IUserAccountRepository : IRepository<User>
{
    public IQueryable<User> Query();

    /// <summary>The account with its roles, tracked, for a roles change.</summary>
    public Task<User?> GetWithRolesAsync(Guid id, CancellationToken cancellationToken);
}
