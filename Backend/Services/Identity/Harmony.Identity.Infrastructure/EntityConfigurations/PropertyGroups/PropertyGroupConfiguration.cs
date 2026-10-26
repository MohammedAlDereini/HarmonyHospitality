using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.PropertyGroups;

/// <summary>
/// The PropertyGroups table of the design. A multi-tenant row (the framework adds the tenant filter and the TenantId
/// index); the code unique per customer; the precedence one column; the type a real key into PropertyGroupTypes.
/// The alternate key (TenantId, Id, TypeCode) is what the brand detail and the member rows point at: their TypeCode
/// copies are held honest by it (the ERD's composite keys). The domain events are not columns.
/// </summary>
public class PropertyGroupConfiguration : IEntityTypeConfiguration<PropertyGroup>
{
    public void Configure(EntityTypeBuilder<PropertyGroup> builder)
    {
        builder.ToTable("PropertyGroups");

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.TypeCode).IsRequired().HasMaxLength(PropertyGroupTypeRef.CodeMaxLength);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(PropertyGroup.CodeMaxLength);
        builder.Property(e => e.ExternalIdentifier).HasMaxLength(PropertyGroup.ExternalIdentifierMaxLength);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(PropertyGroup.NameMaxLength);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(PropertyGroup.NameMaxLength);
        builder.Property(e => e.Description).HasMaxLength(PropertyGroup.DescriptionMaxLength);
        builder.Property(e => e.DisplayOrder).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.OwnsOne(e => e.Precedence, precedence =>
        {
            precedence.Property(p => p.Value).HasColumnName("Precedence").IsRequired();
        });
        builder.Navigation(e => e.Precedence).IsRequired();

        // One code per customer. The handler asks first; this is the answer when two console users race.
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("UX_PropertyGroups_TenantId_Code");

        builder.HasAlternateKey(e => new { e.TenantId, e.Id, e.TypeCode }).HasName("AK_PropertyGroups_TenantId_Id_TypeCode");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PropertyGroupTypeRef>().WithMany().HasForeignKey(e => e.TypeCode).OnDelete(DeleteBehavior.Restrict);
    }
}
