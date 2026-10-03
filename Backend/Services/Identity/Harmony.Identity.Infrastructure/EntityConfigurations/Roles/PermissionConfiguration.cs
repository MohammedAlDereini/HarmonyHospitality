using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Roles;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PermissionValue)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.EndPoint)
            .HasMaxLength(5000);

        builder.Property(p => p.IsActive)
            .IsRequired();

        // One row per enum value per tenant.
        builder.HasIndex(p => new { p.PermissionValue, p.TenantId })
            .IsUnique();
    }
}
