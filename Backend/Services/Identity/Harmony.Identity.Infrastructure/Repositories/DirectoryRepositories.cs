using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

/// <summary>The connection reads name the customer, or the client id when the directory calls and no customer is known yet.</summary>
public class ProvisioningConnectionRepository : BaseRepository<ProvisioningConnection>, IProvisioningConnectionRepository
{
    private readonly IdentityDbContext context;

    public ProvisioningConnectionRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<ProvisioningConnection> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => (await this.context.ProvisioningConnections.FirstOrDefaultAsync(c => c.Id == (Guid)id, cancellationToken))!;

    public Task<ProvisioningConnection?> OfTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        => this.context.ProvisioningConnections
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

    public Task<ProvisioningConnection?> FindByClientIdAsync(string clientId, CancellationToken cancellationToken)
        => this.context.ProvisioningConnections
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);
}

/// <summary>Inside the customer's wall: the directory's call carries its customer, set when the directory proved itself.</summary>
public class DirectoryGroupRepository : BaseRepository<DirectoryGroup>, IDirectoryGroupRepository
{
    private readonly IdentityDbContext context;

    public DirectoryGroupRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<DirectoryGroup> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => (await this.context.DirectoryGroups.FirstOrDefaultAsync(g => g.Id == (Guid)id, cancellationToken))!;

    public IQueryable<DirectoryGroup> Query() => this.context.DirectoryGroups.AsNoTracking();

    public Task<DirectoryGroup?> WithMembersAsync(Guid connectionId, Guid groupId, CancellationToken cancellationToken)
        => this.context.DirectoryGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.ProvisioningConnectionId == connectionId && g.Id == groupId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> MemberIdsAsync(Guid groupId, CancellationToken cancellationToken)
        => await this.context.DirectoryGroupMembers
            .Where(m => m.DirectoryGroupId == groupId)
            .OrderBy(m => m.Id)
            .Select(m => m.UserAccountId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DirectoryGroup>> MappedOfMemberAsync(Guid userAccountId, CancellationToken cancellationToken)
        => await this.context.DirectoryGroups
            .AsNoTracking()
            .Where(g => g.RoleId != null && g.Members.Any(m => m.UserAccountId == userAccountId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DirectoryGroup>> OfMemberAsync(Guid userAccountId, CancellationToken cancellationToken)
        => await this.context.DirectoryGroups
            .Include(g => g.Members)
            .Where(g => g.Members.Any(m => m.UserAccountId == userAccountId))
            .ToListAsync(cancellationToken);
}
