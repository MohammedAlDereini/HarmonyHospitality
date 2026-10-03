using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
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

    public async Task<User?> GetWithRolesAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.context.UserAccounts
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }
}
