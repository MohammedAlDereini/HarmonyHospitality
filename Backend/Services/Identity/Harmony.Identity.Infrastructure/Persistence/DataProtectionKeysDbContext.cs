using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Persistence;

/// <summary>
/// ASP.NET Data Protection's key ring: one table in the Identity database (see AddDataProtectionKeyRing). These keys
/// protect Duende's stored refresh tokens and the authenticator secrets; losing them would log everyone out and break
/// every second factor, so they live with the data, not on the machine.
/// </summary>
public sealed class DataProtectionKeysDbContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeysDbContext(DbContextOptions<DataProtectionKeysDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}
