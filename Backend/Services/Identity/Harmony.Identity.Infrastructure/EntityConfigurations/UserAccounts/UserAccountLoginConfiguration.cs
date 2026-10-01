using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.UserAccounts;

/// <summary>External sign-ins (Microsoft, Google, …): provider + the provider's subject, keyed together.</summary>
public class UserAccountLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("UserAccountLogins");

        builder.HasKey(e => new { e.LoginProvider, e.ProviderKey });

        builder.Property(e => e.LoginProvider).HasMaxLength(128);
        builder.Property(e => e.ProviderKey).HasMaxLength(128);
    }
}
