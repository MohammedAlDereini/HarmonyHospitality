using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.UserAccounts;

/// <summary>The AccessGrants table (ERD): a temporary elevation at one hotel XOR one group, with who and why. Inside the wall.</summary>
public class AccessGrantConfiguration : IEntityTypeConfiguration<AccessGrant>
{
    public void Configure(EntityTypeBuilder<AccessGrant> builder)
    {
        builder.ToTable("AccessGrants", table => table.HasCheckConstraint("CK_AccessGrants_OneScope", "([PropertyId] IS NULL AND [PropertyGroupId] IS NOT NULL) OR ([PropertyId] IS NOT NULL AND [PropertyGroupId] IS NULL)"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.RoleId).IsRequired();
        builder.Property(e => e.FromUtc).IsRequired();
        builder.Property(e => e.ToUtc).IsRequired();
        builder.Property(e => e.GrantedById).IsRequired();
        builder.Property(e => e.Reason).IsRequired().HasMaxLength(AccessGrant.ReasonMaxLength);

        builder.OwnsOne(e => e.Scope, scope =>
        {
            scope.Property(s => s.PropertyId).HasColumnName("PropertyId");
            scope.Property(s => s.PropertyGroupId).HasColumnName("PropertyGroupId");
            scope.HasOne<Property>().WithMany().HasForeignKey(s => s.PropertyId).OnDelete(DeleteBehavior.Restrict);
            scope.HasOne<PropertyGroup>().WithMany().HasForeignKey(s => s.PropertyGroupId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(e => e.Scope).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.UserId, e.ToUtc }).HasDatabaseName("IX_AccessGrants_Account_Window");

        builder.HasOne<User>().WithMany(u => u.AccessGrants).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.GrantedById).OnDelete(DeleteBehavior.Restrict);
    }
}
