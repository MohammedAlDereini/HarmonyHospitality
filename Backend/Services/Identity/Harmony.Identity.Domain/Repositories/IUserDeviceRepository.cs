using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>
/// The browsers each account signed in from. The lookups by account id search every tenant: the sign-in pages run before
/// a tenant is known, and the API checks the account against the caller's tenant first. Query keeps the tenant filter.
/// </summary>
public interface IUserDeviceRepository : IRepository<UserDevice>
{
    /// <summary>The account's device with this key hash, tracked, or null.</summary>
    public Task<UserDevice?> FindByKeyAsync(Guid userId, string keyHash, CancellationToken cancellationToken);

    /// <summary>The account's device with this id, tracked, or null (another account's device is never found).</summary>
    public Task<UserDevice?> FindAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken);

    /// <summary>The devices of the caller's tenant, read-only, to be joined with the account in one query.</summary>
    public IQueryable<UserDevice> Query();

    /// <summary>Lets go of a staged, never-saved device (its insert was refused as a duplicate), so the next save ignores it.</summary>
    public void Forget(UserDevice device);
}
