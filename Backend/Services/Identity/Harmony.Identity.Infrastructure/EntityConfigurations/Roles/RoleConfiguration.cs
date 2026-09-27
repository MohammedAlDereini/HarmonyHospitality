using Harmony.Identity.Domain.Aggregates.RoleModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Roles;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(e => e.NameEn)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.NameAr)
            .HasMaxLength(128);

        builder.Property(e => e.PrivilegeLevel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(e => e.IsSystemRole)
            .IsRequired();

        builder.Property(e => e.IsSuperRole)
            .IsRequired();

        builder.PrimitiveCollection<List<string>>("_permissionCodes")
            .HasColumnName("PermissionCodes")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(e => e.PermissionCodes);
        builder.Ignore(e => e.IsPrivileged);

        builder.HasIndex(e => new { e.TenantId, e.Code })
            .IsUnique();

        builder.HasIndex(e => e.IsSuperRole)
            .IsUnique()
            .HasFilter("[IsSuperRole] = 1");
    }
}