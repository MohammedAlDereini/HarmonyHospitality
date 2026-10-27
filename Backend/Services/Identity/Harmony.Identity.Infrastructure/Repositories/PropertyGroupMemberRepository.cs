using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class PropertyGroupMemberRepository : BaseRepository<PropertyGroupMember>, IPropertyGroupMemberRepository
{
    private readonly IdentityDbContext context;

    public PropertyGroupMemberRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<PropertyGroupMember> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.PropertyGroupMembers.FirstOrDefaultAsync(m => m.Id == (Guid)id, cancellationToken))!;
    }

    public async Task<bool> IsCurrentMemberAsync(Guid groupId, Guid propertyId, CancellationToken cancellationToken)
    {
        return await this.context.PropertyGroupMembers
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(m => m.PropertyGroupId == groupId && m.PropertyId == propertyId && m.Period.ToUtc == null, cancellationToken);
    }

    public async Task<bool> HasCurrentGroupOfTypeAsync(Guid tenantId, Guid propertyId, string typeCode, CancellationToken cancellationToken)
    {
        return await this.context.PropertyGroupMembers
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(m => m.TenantId == tenantId && m.PropertyId == propertyId && m.TypeCode == typeCode && m.Period.ToUtc == null, cancellationToken);
    }

    public async Task<PropertyGroupMember?> FindAcrossTenantsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.context.PropertyGroupMembers
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task AddAsync(PropertyGroupMember member, CancellationToken cancellationToken)
    {
        await this.context.PropertyGroupMembers.AddAsync(member, cancellationToken);
    }

    public async Task<IReadOnlyList<HotelGroup>> GroupsOfHotelAsync(Guid tenantId, Guid propertyId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        // Both walls dropped by name and the customer named instead: the platform resolves a customer's hotel too.
        return await this.context.PropertyGroupMembers
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(m => m.TenantId == tenantId && m.PropertyId == propertyId && m.Period.FromUtc <= at && (m.Period.ToUtc == null || m.Period.ToUtc > at))
            .Join(
                this.context.PropertyGroups.IgnoreQueryFilters([QueryFilterNames.Tenant]).Where(g => g.TenantId == tenantId),
                m => m.PropertyGroupId,
                g => g.Id,
                (m, g) => new HotelGroup(g.Id, g.Code, g.Precedence.Value, g.IsActive))
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public IQueryable<PropertyGroupMember> Query()
    {
        return this.context.PropertyGroupMembers.AsNoTracking();
    }

    public IQueryable<PropertyGroupMember> QueryAcrossTenants()
    {
        return this.context.PropertyGroupMembers
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking();
    }
}
