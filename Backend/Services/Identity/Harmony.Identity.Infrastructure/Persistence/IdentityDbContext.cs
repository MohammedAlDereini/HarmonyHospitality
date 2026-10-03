using Harmony.Core.BuildingBlocks;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbBaseContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<LookupCategory> LookupCategories { get; set; }
    public DbSet<LookupValue> LookupValues { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<CountrySubdivision> CountrySubdivisions { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<TimeZoneRef> TimeZones { get; set; }
    public DbSet<Language> Languages { get; set; }
    public DbSet<CurrencyRef> Currencies { get; set; }
    public DbSet<PropertyTypeRef> PropertyTypes { get; set; }
    public DbSet<PropertyGroupTypeRef> PropertyGroupTypes { get; set; }
    public DbSet<RegulatoryEnvironment> RegulatoryEnvironments { get; set; }
    public DbSet<Capability> Capabilities { get; set; }

    public DbSet<User> UserAccounts { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
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