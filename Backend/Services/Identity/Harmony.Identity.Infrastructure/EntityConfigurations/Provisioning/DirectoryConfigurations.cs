using Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Provisioning;

/// <summary>
/// ERD cluster eight, redrawn for SCIM push (27 October): the connection, the directory's groups with Harmony's role on
/// each, and their members. The directory runs the sync and keeps its own log, so there are no run, lease or bookmark
/// tables, the way Mews and OPERA do it.
/// </summary>
public class ProvisioningConnectionConfiguration : IEntityTypeConfiguration<ProvisioningConnection>
{
    public void Configure(EntityTypeBuilder<ProvisioningConnection> builder)
    {
        builder.ToTable("ProvisioningConnections");
        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);
        builder.Property(e => e.ClientId).IsRequired().HasMaxLength(ProvisioningConnection.ClientIdLength).IsFixedLength().IsUnicode(false);
        builder.Property(e => e.SecretHash).IsRequired().HasMaxLength(ProvisioningConnection.SecretHashLength).IsFixedLength();

        // One per customer (one per Mews subscription); the client id names exactly one connection on the platform.
        builder.HasIndex(e => e.TenantId).IsUnique().HasDatabaseName("UX_ProvisioningConnections_Tenant");
        builder.HasIndex(e => e.ClientId).IsUnique().HasDatabaseName("UX_ProvisioningConnections_ClientId");
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DirectoryGroupConfiguration : IEntityTypeConfiguration<DirectoryGroup>
{
    public void Configure(EntityTypeBuilder<DirectoryGroup> builder)
    {
        // Never both a hotel and a hotel group; a place always comes with a role.
        builder.ToTable("DirectoryGroups", table =>
        {
            table.HasCheckConstraint("CK_DirectoryGroups_OneScope", "[PropertyId] IS NULL OR [PropertyGroupId] IS NULL");
            table.HasCheckConstraint("CK_DirectoryGroups_ScopeHasRole", "[RoleId] IS NOT NULL OR ([PropertyId] IS NULL AND [PropertyGroupId] IS NULL)");
        });
        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.Scope);
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(DirectoryGroup.DisplayNameMaxLength);
        builder.Property(e => e.ExternalId).HasMaxLength(DirectoryGroup.ExternalIdMaxLength);

        // SCIM: displayName is unique; externalId names one group when the directory sends it.
        builder.HasIndex(e => new { e.ProvisioningConnectionId, e.DisplayName }).IsUnique().HasDatabaseName("UX_DirectoryGroups_Connection_Name");
        builder.HasIndex(e => new { e.ProvisioningConnectionId, e.ExternalId }).IsUnique().HasFilter("[ExternalId] IS NOT NULL").HasDatabaseName("UX_DirectoryGroups_Connection_ExternalId");

        builder.HasMany(e => e.Members).WithOne().HasForeignKey(e => e.DirectoryGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProvisioningConnection>().WithMany().HasForeignKey(e => e.ProvisioningConnectionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Property>().WithMany().HasForeignKey(e => e.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PropertyGroup>().WithMany().HasForeignKey(e => e.PropertyGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DirectoryGroupMemberConfiguration : IEntityTypeConfiguration<DirectoryGroupMember>
{
    public void Configure(EntityTypeBuilder<DirectoryGroupMember> builder)
    {
        builder.ToTable("DirectoryGroupMembers");
        builder.HasKey(e => e.Id);

        // A person is in a group once; "which groups is this person in" reads the second index.
        builder.HasIndex(e => new { e.DirectoryGroupId, e.UserAccountId }).IsUnique().HasDatabaseName("UX_DirectoryGroupMembers_Group_Account");
        builder.HasIndex(e => e.UserAccountId).HasDatabaseName("IX_DirectoryGroupMembers_Account");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
