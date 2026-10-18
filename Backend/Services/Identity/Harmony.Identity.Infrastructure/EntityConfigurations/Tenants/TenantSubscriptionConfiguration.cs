using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Tenants;

/// <summary>
/// The TenantSubscriptions table: the customer's contract as lines. Platform data like Tenants, no tenant filter;
/// TenantId names the customer. One line per hotel and module: the unique index counts a NULL hotel as a value, so a
/// chain-wide module can be subscribed once per customer. A line keeps its hotel and its module alive (Restrict); a
/// customer's row is never deleted, but if it were, its lines would go with it (Cascade), as a role's permission rows do.
/// </summary>
public class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("TenantSubscriptions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ModuleCode).IsRequired().HasMaxLength(Module.CodeMaxLength);
        builder.Property(e => e.IsEnabled).IsRequired();
        builder.Property(e => e.DisabledReason).HasMaxLength(500);
        builder.Property(e => e.RenewsAutomatically).IsRequired();
        builder.Property(e => e.StartsOn).IsRequired();

        // HasFilter(null): SQL Server would otherwise exclude NULL hotels from the unique index, and a chain-wide
        // module could be subscribed twice. With no filter, two NULLs count as equal: one chain-wide line per module.
        builder.HasIndex(e => new { e.TenantId, e.PropertyId, e.ModuleCode }).IsUnique().HasFilter(null).HasDatabaseName("UX_TenantSubscriptions_Line");

        builder.HasOne<Tenant>().WithMany(t => t.Subscriptions).HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Property>().WithMany().HasForeignKey(e => e.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Module>().WithMany().HasForeignKey(e => e.ModuleCode).OnDelete(DeleteBehavior.Restrict);
    }
}
