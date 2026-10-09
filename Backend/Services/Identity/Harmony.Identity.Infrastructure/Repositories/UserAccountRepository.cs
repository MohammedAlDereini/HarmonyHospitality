using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class UserAccountRepository : BaseRepository<User>, IUserAccountRepository
{
    private readonly IdentityDbContext context;

    public UserAccountRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<User> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.UserAccounts
            .FirstOrDefaultAsync(a => a.Id == (Guid)id, cancellationToken))!;
    }

    public IQueryable<User> Query()
    {
        return this.context.UserAccounts.AsNoTracking();
    }

    public async Task<IReadOnlyList<User>> FindByUserNameAcrossTenantsAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return await this.context.UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(u => u.NormalizedUserName == normalizedUserName)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetWithRolesAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.context.UserAccounts
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> FindByIdAcrossTenantsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.context.UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<bool> HoldsPrivilegedRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        // The level is stored as text, where "Standard" >= "Manager": ask for the privileged names, never ">=" in SQL.
        var privileged = Enum.GetValues<PrivilegeLevel>().Where(level => level >= PrivilegeLevel.Manager).ToList();

        return await this.context.UserRoles
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(ur => ur.UserId == userId && privileged.Contains(ur.Role.PrivilegeLevel), cancellationToken);
    }

    public async Task<int> RankOfAsync(Guid userId, CancellationToken cancellationToken)
    {
        // The level is stored as text, where "Standard" >= "Manager": fetch the names, rank in memory, never ">=" in SQL.
        var held = await this.context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => new { ur.Role.PrivilegeLevel, ur.Role.IsSuperRole })
            .ToListAsync(cancellationToken);

        return held.Count == 0 ? RoleRank.None : held.Max(r => RoleRank.Of(r.PrivilegeLevel, r.IsSuperRole));
    }

    public async Task<bool> SoleAdministratorRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        // Equality on the stored text is safe (only ordering is not). Suspended and terminated accounts hold no fort.
        var administrators = await this.context.UserRoles
            .Where(ur => ur.User.State == AdministrativeState.Active)
            .Where(ur => ur.Role.PrivilegeLevel == PrivilegeLevel.Administrator || ur.Role.IsSuperRole)
            .Select(ur => new { ur.UserId, ur.RoleId })
            .ToListAsync(cancellationToken);

        return administrators.Count > 0 && administrators.All(a => a.RoleId == roleId);
    }
}
