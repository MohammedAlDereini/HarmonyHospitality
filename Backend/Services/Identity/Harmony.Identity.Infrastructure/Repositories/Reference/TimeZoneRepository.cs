using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

public class TimeZoneRepository : ReferenceRepositoryBase<TimeZoneRef>, ITimeZoneRepository
{
    public TimeZoneRepository(IdentityDbContext context)
        : base(context)
    {
    }

    public async Task<TimeZoneRef?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return await this.Context.TimeZones.FirstOrDefaultAsync(z => z.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
    {
        return await this.Context.TimeZones.AnyAsync(z => z.Id == id, cancellationToken);
    }

    public async Task AddAsync(TimeZoneRef timeZone, CancellationToken cancellationToken)
    {
        await this.Context.TimeZones.AddAsync(timeZone, cancellationToken);
    }

    public IQueryable<TimeZoneRef> Query()
    {
        return this.Context.TimeZones.AsNoTracking();
    }
}
