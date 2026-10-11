using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>
/// The passkeys each account holds. The lookups by account id search every tenant: the sign-in page runs before a tenant
/// is known, and the API checks the account against the caller's tenant first. Query keeps the tenant filter.
/// </summary>
public interface IUserPasskeyRepository : IRepository<UserPasskey>
{
    /// <summary>The account's passkey with this id, tracked, or null (another account's passkey is never found).</summary>
    public Task<UserPasskey?> FindAsync(Guid userId, Guid passkeyId, CancellationToken cancellationToken);

    /// <summary>The account's passkey with this credential id, tracked, or null.</summary>
    public Task<UserPasskey?> FindByCredentialAsync(Guid userId, byte[] credentialId, CancellationToken cancellationToken);

    /// <summary>The passkey with this credential id whoever holds it, read-only, or null: a refused sign-in names only the key.</summary>
    public Task<UserPasskey?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken);

    /// <summary>How many passkeys the account holds.</summary>
    public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The passkeys of the caller's tenant, read-only, to be joined with the account in one query.</summary>
    public IQueryable<UserPasskey> Query();
}
