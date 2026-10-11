using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class UserPasskeyRepository : BaseRepository<UserPasskey>, IUserPasskeyRepository
{
    private readonly IdentityDbContext context;

    public UserPasskeyRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public async Task<UserPasskey?> FindAsync(Guid userId, Guid passkeyId, CancellationToken cancellationToken)
    {
        return await this.context.UserPasskeys
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(p => p.UserAccountId == userId && p.Id == passkeyId, cancellationToken);
    }

    public async Task<UserPasskey?> FindByCredentialAsync(Guid userId, byte[] credentialId, CancellationToken cancellationToken)
    {
        return await this.context.UserPasskeys
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(p => p.UserAccountId == userId && p.CredentialId == credentialId, cancellationToken);
    }

    public async Task<UserPasskey?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        return await this.context.UserPasskeys
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.CredentialId == credentialId, cancellationToken);
    }

    public async Task<int> CountAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await this.context.UserPasskeys
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .CountAsync(p => p.UserAccountId == userId, cancellationToken);
    }

    public IQueryable<UserPasskey> Query()
    {
        return this.context.UserPasskeys.AsNoTracking();
    }
}
