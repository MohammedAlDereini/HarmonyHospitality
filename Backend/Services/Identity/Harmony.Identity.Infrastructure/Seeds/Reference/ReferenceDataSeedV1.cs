using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Seeds.Reference;

internal sealed class ReferenceDataSeedV1 : IDbSeed<IdentityDbContext>
{
    public int Version => 1;

    public async Task SeedAsync(IdentityDbContext dbContext, Tenant tenant, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await AddMissingAsync(dbContext.Currencies, ReferenceDataV1.Currencies(), c => c.Code, cancellationToken);
        await AddMissingAsync(dbContext.TimeZones, ReferenceDataV1.TimeZones(), z => z.Id, cancellationToken);
        await AddMissingAsync(dbContext.Languages, ReferenceDataV1.Languages(), l => l.Code, cancellationToken);
        await AddMissingAsync(dbContext.Countries, ReferenceDataV1.Countries(), c => c.Code, cancellationToken);
        await AddMissingAsync(dbContext.CountrySubdivisions, ReferenceDataV1.Subdivisions(), s => s.Code, cancellationToken);
        await AddMissingAsync(dbContext.Cities, ReferenceDataV1.Cities(), c => c.Id, cancellationToken);
        await AddMissingAsync(dbContext.PropertyTypes, ReferenceDataV1.PropertyTypes(), t => t.Code, cancellationToken);
        await AddMissingAsync(dbContext.PropertyGroupTypes, ReferenceDataV1.PropertyGroupTypes(), t => t.Code, cancellationToken);
        await AddMissingAsync(dbContext.Capabilities, ReferenceDataV1.Capabilities(), c => c.Code, cancellationToken);

        // One save: EF orders the inserts by their foreign keys.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Adds only rows that are not there yet, so it is safe to run again
    // and never overwrites a value someone has since changed.
    private static async Task AddMissingAsync<T, TKey>(DbSet<T> set, IReadOnlyList<T> rows, Func<T, TKey> key, CancellationToken cancellationToken)
        where T : class
    {
        var existing = (await set.AsNoTracking().ToListAsync(cancellationToken)).Select(key).ToHashSet();

        await set.AddRangeAsync(rows.Where(r => !existing.Contains(key(r))), cancellationToken);
    }
}
