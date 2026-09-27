using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Infrastructure.Seeds.Roles;

namespace Harmony.Identity.Infrastructure.Persistence;

public class IdentityDbContextSeed : IDbSeeds<IdentityDbContext>
{
    public List<IDbSeed<IdentityDbContext>> CommonSeeds()
    {
        return [];
    }

    public List<IDbSeed<IdentityDbContext>> TenantSeeds()
    {
        return
        [
            new SuperAdminRoleSeedV1(),
        ];
    }
}