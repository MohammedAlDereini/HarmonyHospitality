using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.PropertyGroups;

/// <summary>
/// The BrandDetails table: brand-only columns hung off the group's own key. The CHECK and the composite key against
/// the group's (TenantId, Id, TypeCode) mean a detail on a Region group fails in SQL, not in code review. Inside the
/// wall (TenantId). The group's one detail goes with it (Cascade); clearing it on the group deletes the row.
/// </summary>
public class BrandDetailConfiguration : IEntityTypeConfiguration<BrandDetail>
{
    public void Configure(EntityTypeBuilder<BrandDetail> builder)
    {
        builder.ToTable("BrandDetails", table => table.HasCheckConstraint("CK_BrandDetails_TypeCode", $"[TypeCode] = '{PropertyGroupTypeRef.BrandCode}'"));

        builder.HasKey(e => e.PropertyGroupId);
        builder.Property(e => e.PropertyGroupId).ValueGeneratedNever();

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.TypeCode).IsRequired().HasMaxLength(PropertyGroupTypeRef.CodeMaxLength);
        builder.Property(e => e.ThemeReference).HasMaxLength(BrandDetail.ThemeReferenceMaxLength);
        builder.Property(e => e.WebsiteUrl).HasMaxLength(BrandDetail.WebsiteUrlMaxLength);
        builder.Property(e => e.FranchisorName).HasMaxLength(BrandDetail.FranchisorNameMaxLength);
        builder.Property(e => e.RoyaltyPercent).HasPrecision(5, 2);

        builder.HasOne<PropertyGroup>()
            .WithOne(g => g.Brand)
            .HasForeignKey<BrandDetail>(e => new { e.TenantId, e.PropertyGroupId, e.TypeCode })
            .HasPrincipalKey<PropertyGroup>(g => new { g.TenantId, g.Id, g.TypeCode })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
