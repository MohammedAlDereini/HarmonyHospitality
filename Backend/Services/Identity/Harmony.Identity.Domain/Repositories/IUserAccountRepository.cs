using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>Reads only. Writes go through ASP.NET Core Identity's UserManager, which validates and saves.</summary>
public interface IUserAccountRepository : IRepository<HarmonyUser>
{
    public IQueryable<HarmonyUser> Query();

    /// <summary>
    /// A machine account by tenant and code, for the token endpoint: that request is anonymous, so the
    /// tenant comes from the request itself rather than from the call context.
    /// </summary>
    public Task<HarmonyUser?> FindServicePrincipalAsync(Guid tenantId, string code, CancellationToken cancellationToken);
}
