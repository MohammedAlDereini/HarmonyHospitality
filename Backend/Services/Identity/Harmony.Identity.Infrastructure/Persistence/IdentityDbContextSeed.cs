using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Infrastructure.Seeds.Permissions;
using Harmony.Identity.Infrastructure.Seeds.Roles;
using Harmony.Identity.Infrastructure.Seeds.Users;

namespace Harmony.Identity.Infrastructure.Persistence;

public class IdentityDbContextSeed : IDbSeeds<IdentityDbContext>
{
    public List<IDbSeed<IdentityDbContext>> CommonSeeds()
    {
        // Nothing: the reference tables are filled through the Reference API by the platform admin, not by seeds.
        return [];
    }

    public List<IDbSeed<IdentityDbContext>> TenantSeeds()
    {
        return
        [
            new PermissionSeedV1(),
            new SuperAdminRoleSeedV1(),
            new SuperAdminUserSeedV1(),
        ];
    }
}
