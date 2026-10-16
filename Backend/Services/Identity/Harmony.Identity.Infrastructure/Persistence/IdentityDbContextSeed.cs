using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Infrastructure.Seeds.Permissions;
using Harmony.Identity.Infrastructure.Seeds.Roles;
using Harmony.Identity.Infrastructure.Seeds.Tenants;
using Harmony.Identity.Infrastructure.Seeds.Users;

namespace Harmony.Identity.Infrastructure.Persistence;

public class IdentityDbContextSeed : IDbSeeds<IdentityDbContext>
{
    public List<IDbSeed<IdentityDbContext>> CommonSeeds()
    {
        // Platform data: the platform's own Tenants row. The reference tables are filled through the Reference API by
        // the platform admin, not by seeds.
        return [new PlatformTenantSeedV1()];
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
