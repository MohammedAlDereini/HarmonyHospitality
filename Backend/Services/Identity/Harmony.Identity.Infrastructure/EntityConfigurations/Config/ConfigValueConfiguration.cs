using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Config;

/// <summary>
/// The ConfigValues table of the design. A multi-tenant row (the framework adds the tenant filter and the TenantId
/// index). One value per key per place: the unique index. The key is a real foreign key into ConfigSettings; the place
/// cannot be one (polymorphic), so two CHECKs hold what SQL can: the level is one of three, and a customer-wide value
/// sits on the customer's own id.
/// </summary>
public class ConfigValueConfiguration : IEntityTypeConfiguration<ConfigValue>
{
    public void Configure(EntityTypeBuilder<ConfigValue> builder)
    {
        builder.ToTable("ConfigValues", table =>
        {
            table.HasCheckConstraint("CK_ConfigValues_Scope", "[Scope] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_ConfigValues_TenantScope", "[Scope] <> 1 OR [ScopeId] = [TenantId]");
        });

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.Place);
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.Scope).IsRequired();
        builder.Property(e => e.ScopeId).IsRequired();
        builder.Property(e => e.Key).IsRequired().HasMaxLength(ConfigSetting.KeyMaxLength);
        builder.Property(e => e.Value).IsRequired().HasMaxLength(ConfigValue.ValueMaxLength);

        // One value per key per place. The handler finds and changes the row first; this is the answer when two people race.
        builder.HasIndex(e => new { e.TenantId, e.Scope, e.ScopeId, e.Key }).IsUnique().HasDatabaseName("UX_ConfigValues_Place_Key");

        // The resolver's read: a customer's values for a key at a set of places.
        builder.HasIndex(e => new { e.TenantId, e.Key }).HasDatabaseName("IX_ConfigValues_TenantId_Key");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigSetting>().WithMany().HasForeignKey(e => e.Key).OnDelete(DeleteBehavior.Restrict);
    }
}
