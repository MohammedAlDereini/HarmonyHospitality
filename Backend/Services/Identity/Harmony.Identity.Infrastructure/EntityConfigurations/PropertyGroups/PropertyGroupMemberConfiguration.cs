using Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.PropertyGroups;

/// <summary>
/// The PropertyGroupMembers table: the dated join that makes the design work. Inside the wall (TenantId). The period
/// is two columns of the same row. One brand per hotel is the filtered unique index: the TypeCode copy on the row is
/// what makes it a single-table rule, and the composite key against the group keeps the copy honest. Other exclusive
/// types (Owner) are held by the handler only. A member row keeps its group and its hotel alive (Restrict).
/// </summary>
public class PropertyGroupMemberConfiguration : IEntityTypeConfiguration<PropertyGroupMember>
{
    public void Configure(EntityTypeBuilder<PropertyGroupMember> builder)
    {
        builder.ToTable("PropertyGroupMembers");

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.PropertyGroupId).IsRequired();
        builder.Property(e => e.PropertyId).IsRequired();
        builder.Property(e => e.TypeCode).IsRequired().HasMaxLength(PropertyGroupTypeRef.CodeMaxLength);

        builder.OwnsOne(e => e.Period, period =>
        {
            period.Property(p => p.FromUtc).HasColumnName("FromUtc").IsRequired();
            period.Property(p => p.ToUtc).HasColumnName("ToUtc");
        });
        builder.Navigation(e => e.Period).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.PropertyId })
            .IsUnique()
            .HasFilter($"[ToUtc] IS NULL AND [TypeCode] = '{PropertyGroupTypeRef.BrandCode}'")
            .HasDatabaseName("UX_PropertyGroupMembers_OneBrandPerHotel");

        builder.HasOne<PropertyGroup>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.PropertyGroupId, e.TypeCode })
            .HasPrincipalKey(g => new { g.TenantId, g.Id, g.TypeCode })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Property>().WithMany().HasForeignKey(e => e.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PropertyGroupTypeRef>().WithMany().HasForeignKey(e => e.TypeCode).OnDelete(DeleteBehavior.Restrict);
    }
}
