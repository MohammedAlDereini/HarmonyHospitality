using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Infrastructure.Seeds.Permissions;
using Harmony.Identity.Infrastructure.Seeds.Reference;
using Harmony.Identity.Infrastructure.Seeds.Roles;

namespace Harmony.Identity.Infrastructure.Persistence;

public class IdentityDbContextSeed : IDbSeeds<IdentityDbContext>
{
    public List<IDbSeed<IdentityDbContext>> CommonSeeds()
    {
        return
        [
            new ReferenceDataSeedV1(),
        ];
    }

    public List<IDbSeed<IdentityDbContext>> TenantSeeds()
    {
        return
        [
            new PermissionSeedV1(),
            new SuperAdminRoleSeedV1(),
        ];
    }
}
