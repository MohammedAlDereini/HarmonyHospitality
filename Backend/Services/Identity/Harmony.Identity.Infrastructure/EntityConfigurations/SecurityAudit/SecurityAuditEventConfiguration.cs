using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.SecurityAudit;

/// <summary>
/// The security trail (design docs: SecurityAuditEvents). No audit or rowversion columns: rows are written once and never
/// updated. Action is free text on purpose (no lookup, no foreign key): a lost audit row is the one unforgivable drop.
/// </summary>
public class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("SecurityAuditEvents");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Action).IsRequired().HasMaxLength(SecurityAuditEvent.ActionMaxLength);
        builder.Property(e => e.ActorSubject).IsRequired().HasMaxLength(SecurityAuditEvent.ActorMaxLength);
        builder.Property(e => e.SourceAddress).HasMaxLength(SecurityAuditEvent.AddressMaxLength);
        builder.Property(e => e.Device).HasMaxLength(SecurityAuditEvent.DeviceMaxLength);
        builder.Property(e => e.OccurredAtUtc).IsRequired();

        // An account is never deleted (terminated only), so its trail can point at it; Restrict keeps it that way.
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Restrict);

        // "This account's history, newest first."
        builder.HasIndex(e => new { e.UserAccountId, e.OccurredAtUtc }).HasDatabaseName("IX_SecurityAuditEvents_Account_Time");
    }
}
