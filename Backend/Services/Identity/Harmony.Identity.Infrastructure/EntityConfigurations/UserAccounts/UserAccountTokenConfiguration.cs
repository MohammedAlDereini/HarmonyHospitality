using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.UserAccounts;

/// <summary>Identity's per-user tokens (authenticator keys, recovery codes, …), as Identity expects them.</summary>
public class UserAccountTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("UserAccountTokens");

        builder.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

        builder.Property(e => e.LoginProvider).HasMaxLength(128);
        builder.Property(e => e.Name).HasMaxLength(128);
    }
}
