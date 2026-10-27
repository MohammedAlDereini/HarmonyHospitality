using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.UserAccounts;

/// <summary>
/// ASP.NET Core Identity's user table, configured by hand because our DbContext is the framework's
/// DbBaseContext (tenant filter, audit, rowversion), not Identity's. Same shape Identity expects,
/// with one change: user names and emails are unique per tenant, not platform-wide.
/// </summary>
public class UserAccountConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("UserAccounts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserName).HasMaxLength(256);
        builder.Property(e => e.NormalizedUserName).HasMaxLength(256);
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.NormalizedEmail).HasMaxLength(256);

        // Identity's own optimistic concurrency, beside the framework's rowversion.
        builder.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(e => e.StateReason)
            .HasMaxLength(500);

        builder.Property(e => e.SecurityVersion)
            .IsRequired();

        builder.Property(e => e.MustChangePassword)
            .IsRequired();

        // How the second step is proved (two-step itself is Identity's TwoFactorEnabled). Text, like State, so a row reads
        // plainly. Existing accounts get AuthenticatorApp: the only method before this column.
        builder.Property(e => e.TwoStepMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(TwoStepMethod.AuthenticatorApp);

        // The two-step exception list: the admin's reason, or null when the person is not on it. Same length as StateReason.
        builder.Property(e => e.TwoStepExemptReason)
            .HasMaxLength(500);

        // The ERD's identity of the account: where it was made and its id there. Every account so far was made here.
        builder.Property(e => e.Issuer)
            .IsRequired()
            .HasMaxLength(FederatedAuthority.IssuerMaxLength)
            .HasDefaultValue(FederatedAuthority.LocalIssuer);

        builder.Property(e => e.Subject)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(e => new { e.Issuer, e.Subject })
            .IsUnique()
            .HasDatabaseName("UX_UserAccounts_Issuer_Subject");

        builder.Property(e => e.IsServicePrincipal)
            .IsRequired();

        // Two-step set up, read straight from Identity's own column, so the two can never disagree.
        builder.Property(e => e.IsMfaEnrolled)
            .HasComputedColumnSql("[TwoFactorEnabled]", stored: true);

        // The account this one was merged into (stage D). An account is never deleted while another names it.
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SupersededByUserAccountId).OnDelete(DeleteBehavior.Restrict);

        // The administrator's exception: this person keeps a Harmony password where the company login covers them.
        builder.Property(e => e.PasswordSignIn)
            .IsRequired();

        builder.Ignore(e => e.CanSignIn);
        builder.Ignore(e => e.IsTwoStepExempt);

        // One e-mail is one person on the whole platform (decided 2026-10-16): the sign-in page has no tenant field, so an
        // address must name exactly one account wherever it lives. A person who works for two tenants is a later ticket
        // (memberships), never a second account.
        builder.HasIndex(e => e.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("UX_UserAccounts_UserName");

        builder.HasIndex(e => new { e.TenantId, e.NormalizedEmail })
            .HasDatabaseName("IX_UserAccounts_Tenant_Email");

        builder.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(c => c.UserId).IsRequired();
        builder.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(l => l.UserId).IsRequired();
        builder.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(t => t.UserId).IsRequired();
    }
}
