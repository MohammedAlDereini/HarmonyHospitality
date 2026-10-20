using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Properties;

/// <summary>
/// The PropertyContacts table: the named people at a hotel, inside the wall like the hotel (TenantId is the wall
/// column). A hotel's contacts go with it (Cascade). The filtered unique index keeps one primary per hotel in SQL,
/// whatever the code does.
/// </summary>
public class PropertyContactConfiguration : IEntityTypeConfiguration<PropertyContact>
{
    public void Configure(EntityTypeBuilder<PropertyContact> builder)
    {
        builder.ToTable("PropertyContacts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.PropertyId).IsRequired();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(PropertyContact.NameMaxLength);
        builder.Property(e => e.Role).HasMaxLength(PropertyContact.RoleMaxLength);
        builder.Property(e => e.Email).HasMaxLength(PropertyContact.EmailMaxLength);
        builder.Property(e => e.Telephone).HasMaxLength(PropertyContact.TelephoneMaxLength);
        builder.Property(e => e.IsPrimary).IsRequired();

        builder.HasIndex(e => e.PropertyId).IsUnique().HasFilter("[IsPrimary] = 1").HasDatabaseName("UX_PropertyContacts_Primary");

        builder.HasOne<Property>().WithMany(p => p.Contacts).HasForeignKey(e => e.PropertyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
