using Harmony.Identity.Domain.Entities.Aggregates.RevocationModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Revocation;

/// <summary>ERD cluster six: the feed, its clock, snapshots and tombstones. Rows are written by Identity only, never updated (except the clock).</summary>
public class RevocationSequenceConfiguration : IEntityTypeConfiguration<RevocationSequence>
{
    public void Configure(EntityTypeBuilder<RevocationSequence> builder)
    {
        builder.ToTable("AuthorizationRevocationSequence", table =>
            table.HasCheckConstraint("CK_AuthorizationRevocationSequence_Order", "[MinimumRetainedSequence] >= 1 AND [MinimumRetainedSequence] <= [HighWatermark] + 1"));
        builder.HasKey(e => e.TenantId);
        builder.Property(e => e.HighWatermark).IsRequired();
        builder.Property(e => e.MinimumRetainedSequence).IsRequired();
        builder.HasOne<Tenant>().WithOne().HasForeignKey<RevocationSequence>(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RevocationEntryConfiguration : IEntityTypeConfiguration<RevocationEntry>
{
    public void Configure(EntityTypeBuilder<RevocationEntry> builder)
    {
        builder.ToTable("AuthorizationRevocationFeed");
        builder.HasKey(e => new { e.TenantId, e.Sequence });
        builder.Property(e => e.Kind).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.NewSecurityVersion).IsRequired();
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.OccurredAtUtc }).HasDatabaseName("IX_AuthorizationRevocationFeed_Tenant_Time");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SecurityOperation>().WithMany().HasForeignKey(e => e.CorrelationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RevocationSnapshotConfiguration : IEntityTypeConfiguration<RevocationSnapshot>
{
    public void Configure(EntityTypeBuilder<RevocationSnapshot> builder)
    {
        builder.ToTable("RevocationSnapshots");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.TenantId, e.TakenAtSequence }).HasDatabaseName("IX_RevocationSnapshots_Tenant_Sequence");
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RevocationSnapshotItemConfiguration : IEntityTypeConfiguration<RevocationSnapshotItem>
{
    public void Configure(EntityTypeBuilder<RevocationSnapshotItem> builder)
    {
        builder.ToTable("RevocationSnapshotItems");
        builder.HasKey(e => new { e.SnapshotId, e.PageKey });
        builder.HasOne<RevocationSnapshot>().WithMany().HasForeignKey(e => e.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RevocationGapTombstoneConfiguration : IEntityTypeConfiguration<RevocationGapTombstone>
{
    public void Configure(EntityTypeBuilder<RevocationGapTombstone> builder)
    {
        builder.ToTable("RevocationGapTombstones");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reason).IsRequired().HasMaxLength(200);
        builder.HasIndex(e => new { e.TenantId, e.ThroughSequence }).HasDatabaseName("IX_RevocationGapTombstones_Tenant_Through");
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SecurityOperationConfiguration : IEntityTypeConfiguration<SecurityOperation>
{
    public void Configure(EntityTypeBuilder<SecurityOperation> builder)
    {
        builder.ToTable("SecurityOperations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).IsRequired().HasMaxLength(SecurityOperation.KindMaxLength);
        builder.HasIndex(e => new { e.TenantId, e.OccurredAtUtc }).HasDatabaseName("IX_SecurityOperations_Tenant_Time");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
