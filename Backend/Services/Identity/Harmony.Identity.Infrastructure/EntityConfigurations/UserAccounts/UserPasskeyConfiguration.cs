using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.UserAccounts;

/// <summary>The passkeys each account holds. One row per credential; a credential id is unique across every account.</summary>
public class UserPasskeyConfiguration : IEntityTypeConfiguration<UserPasskey>
{
    public void Configure(EntityTypeBuilder<UserPasskey> builder)
    {
        builder.ToTable("UserPasskeys");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.CredentialId).IsRequired().HasMaxLength(UserPasskey.CredentialIdMaxLength);
        builder.Property(e => e.PublicKey).IsRequired();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(UserPasskey.NameMaxLength);
        builder.Property(e => e.Transports).HasMaxLength(UserPasskey.TransportsMaxLength);
        builder.Property(e => e.AttestationObject).IsRequired();
        builder.Property(e => e.ClientDataJson).IsRequired();

        // A passkey belongs to its account; removing the passkey never touches the account.
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Cascade);

        // Sign-in finds the account by the credential id alone, before any tenant is known.
        builder.HasIndex(e => e.CredentialId).IsUnique().HasDatabaseName("UX_UserPasskeys_Credential");
        builder.HasIndex(e => e.UserAccountId).HasDatabaseName("IX_UserPasskeys_Account");
    }
}
