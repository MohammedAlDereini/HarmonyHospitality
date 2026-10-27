using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class ConfigSettingRepository : BaseRepository<ConfigSetting>, IConfigSettingRepository
{
    private readonly IdentityDbContext context;

    public ConfigSettingRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<ConfigSetting> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.FindAsync((string)id, cancellationToken))!;
    }

    public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken)
    {
        // The column compares without case (the database collation): frontoffice.checkInTime and FRONTOFFICE.CHECKINTIME are one key.
        return this.context.ConfigSettings.AnyAsync(s => s.Id == key, cancellationToken);
    }

    public Task<ConfigSetting?> FindAsync(string key, CancellationToken cancellationToken)
    {
        return this.context.ConfigSettings.FirstOrDefaultAsync(s => s.Id == key, cancellationToken);
    }

    public async Task AddAsync(ConfigSetting setting, CancellationToken cancellationToken)
    {
        await this.context.ConfigSettings.AddAsync(setting, cancellationToken);
    }

    public IQueryable<ConfigSetting> Query()
    {
        return this.context.ConfigSettings.AsNoTracking();
    }
}
