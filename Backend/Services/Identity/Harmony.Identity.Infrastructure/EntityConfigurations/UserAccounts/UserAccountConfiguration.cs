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

        builder.Ignore(e => e.CanSignIn);

        builder.HasIndex(e => new { e.TenantId, e.NormalizedUserName })
            .IsUnique()
            .HasDatabaseName("UX_UserAccounts_Tenant_UserName");

        builder.HasIndex(e => new { e.TenantId, e.NormalizedEmail })
            .HasDatabaseName("IX_UserAccounts_Tenant_Email");

        builder.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(c => c.UserId).IsRequired();
        builder.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(l => l.UserId).IsRequired();
        builder.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(t => t.UserId).IsRequired();
    }
}
