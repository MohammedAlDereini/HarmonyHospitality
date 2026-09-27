using Harmony.Core.BuildingBlocks;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Aggregates.LookupModule;
using Microsoft.EntityFrameworkCore;
using Harmony.Identity.Domain.Aggregates.RoleModule;

namespace Harmony.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbBaseContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<LookupCategory> LookupCategories { get; set; }
    public DbSet<LookupValue> LookupValues { get; set; }
    public DbSet<Role> Roles { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplySeedingConfiguration();
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    }
}