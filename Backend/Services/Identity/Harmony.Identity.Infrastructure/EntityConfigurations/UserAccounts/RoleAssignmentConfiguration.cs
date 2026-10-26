using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.UserAccounts;

/// <summary>
/// The RoleAssignments table (ERD): a role at one hotel XOR one group, the CHECK holding what the value object
/// promises. Inside the wall. An account's assignments go with it (Cascade); a role, a hotel or a group stays alive
/// while an assignment names it (Restrict).
/// </summary>
public class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("RoleAssignments", table => table.HasCheckConstraint("CK_RoleAssignments_OneScope", "([PropertyId] IS NULL AND [PropertyGroupId] IS NOT NULL) OR ([PropertyId] IS NOT NULL AND [PropertyGroupId] IS NULL)"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.RoleId).IsRequired();

        builder.OwnsOne(e => e.Scope, scope =>
        {
            scope.Property(s => s.PropertyId).HasColumnName("PropertyId");
            scope.Property(s => s.PropertyGroupId).HasColumnName("PropertyGroupId");
            scope.HasOne<Property>().WithMany().HasForeignKey(s => s.PropertyId).OnDelete(DeleteBehavior.Restrict);
            scope.HasOne<PropertyGroup>().WithMany().HasForeignKey(s => s.PropertyGroupId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(e => e.Scope).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.UserId, e.EndsAtUtc }).HasDatabaseName("IX_RoleAssignments_Account_Open");

        builder.HasOne<User>().WithMany(u => u.RoleAssignments).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
