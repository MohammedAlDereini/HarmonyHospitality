using Harmony.Identity.Domain.Entities.Aggregates.EnrolmentModule;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.SignIn;

/// <summary>The LocalStaffEnrolments table (ERD cluster five): invitations, the e-mail only as a digest.</summary>
public class LocalStaffEnrolmentConfiguration : IEntityTypeConfiguration<LocalStaffEnrolment>
{
    public void Configure(EntityTypeBuilder<LocalStaffEnrolment> builder)
    {
        builder.ToTable("LocalStaffEnrolments", table =>
            table.HasCheckConstraint("CK_LocalStaffEnrolments_State", "[State] IN (N'Pending', N'Redeemed', N'Cancelled')"));

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.SponsorUserAccountId).IsRequired();
        builder.Property(e => e.FederatedAuthorityId).IsRequired();
        builder.Property(e => e.UserAccountId).IsRequired();
        builder.Property(e => e.InviteeHandleDigest).IsRequired().HasMaxLength(LocalStaffEnrolment.DigestLength).IsFixedLength();
        builder.Property(e => e.State).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.ExpiresAtUtc).IsRequired();

        // At most one open invitation per account: a resend cancels the old row in the same save.
        builder.HasIndex(e => e.UserAccountId).IsUnique().HasFilter("[State] = N'Pending'").HasDatabaseName("UX_LocalStaffEnrolments_OnePending");
        builder.HasIndex(e => new { e.TenantId, e.State }).HasDatabaseName("IX_LocalStaffEnrolments_Tenant_State");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SponsorUserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FederatedAuthority>().WithMany().HasForeignKey(e => e.FederatedAuthorityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
