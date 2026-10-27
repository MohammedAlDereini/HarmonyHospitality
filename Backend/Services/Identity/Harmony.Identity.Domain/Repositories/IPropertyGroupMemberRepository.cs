using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>The memberships. Walled rows; the platform asks from outside with the tenant filter switched off.</summary>
public interface IPropertyGroupMemberRepository : IRepository<PropertyGroupMember>
{
    /// <summary>Is the hotel a current member of this group.</summary>
    Task<bool> IsCurrentMemberAsync(Guid groupId, Guid propertyId, CancellationToken cancellationToken);

    /// <summary>Is the hotel a current member of any group of this type (the exclusive-type rule, asked before the insert).</summary>
    Task<bool> HasCurrentGroupOfTypeAsync(Guid tenantId, Guid propertyId, string typeCode, CancellationToken cancellationToken);

    /// <summary>The membership, tracked, for a change by the platform; null when unknown.</summary>
    Task<PropertyGroupMember?> FindAcrossTenantsAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(PropertyGroupMember member, CancellationToken cancellationToken);

    /// <summary>
    /// The groups a customer's hotel is in at a moment (the period covers it), with each group's precedence and whether it
    /// is switched on: what the settings resolver reads. The customer is named, so the platform reads through the same door.
    /// </summary>
    Task<IReadOnlyList<HotelGroup>> GroupsOfHotelAsync(Guid tenantId, Guid propertyId, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>The caller's own memberships, untracked, inside the wall.</summary>
    IQueryable<PropertyGroupMember> Query();

    /// <summary>Every customer's memberships, untracked, for the console's reads.</summary>
    IQueryable<PropertyGroupMember> QueryAcrossTenants();
}
