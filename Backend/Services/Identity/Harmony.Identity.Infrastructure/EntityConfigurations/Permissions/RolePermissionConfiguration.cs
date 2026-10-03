using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Permissions;

/// <summary>
/// PropX's RolePermissions mapping. One difference: deleting a role deletes its rows (Cascade); a permission in use
/// cannot be deleted (Restrict). Audit, tenant and rowversion columns come from the framework conventions.
/// </summary>
public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(rp => rp.Id);

        builder.Property(rp => rp.RoleId)
            .IsRequired();

        builder.Property(rp => rp.PermissionId)
            .IsRequired();

        // No duplicate assignment.
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId, rp.TenantId })
            .IsUnique();

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.Permissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
