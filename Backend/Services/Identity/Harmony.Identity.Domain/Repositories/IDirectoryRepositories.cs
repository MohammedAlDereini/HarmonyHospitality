using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>
/// The customers' directory connections (stage E). The directory calls before any customer is known, so the client id is
/// read across customers; everything else names the customer.
/// </summary>
public interface IProvisioningConnectionRepository : IRepository<ProvisioningConnection>
{
    /// <summary>The customer's connection, tracked; null when it has none.</summary>
    Task<ProvisioningConnection?> OfTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The connection with this client id in any customer, untracked; null when none has it.</summary>
    Task<ProvisioningConnection?> FindByClientIdAsync(string clientId, CancellationToken cancellationToken);
}

/// <summary>The directory's groups and their members, inside the customer's wall.</summary>
public interface IDirectoryGroupRepository : IRepository<DirectoryGroup>
{
    /// <summary>The groups, untracked, without members.</summary>
    IQueryable<DirectoryGroup> Query();

    /// <summary>One group of the connection with its members, tracked; null when it is not that connection's.</summary>
    Task<DirectoryGroup?> WithMembersAsync(Guid connectionId, Guid groupId, CancellationToken cancellationToken);

    /// <summary>The ids of the members of one group, oldest first.</summary>
    Task<IReadOnlyList<Guid>> MemberIdsAsync(Guid groupId, CancellationToken cancellationToken);

    /// <summary>The groups with a role that the account is in, untracked: what the account holds.</summary>
    Task<IReadOnlyList<DirectoryGroup>> MappedOfMemberAsync(Guid userAccountId, CancellationToken cancellationToken);

    /// <summary>Every membership row of the account, tracked, for an account the directory deleted.</summary>
    Task<IReadOnlyList<DirectoryGroup>> OfMemberAsync(Guid userAccountId, CancellationToken cancellationToken);
}
