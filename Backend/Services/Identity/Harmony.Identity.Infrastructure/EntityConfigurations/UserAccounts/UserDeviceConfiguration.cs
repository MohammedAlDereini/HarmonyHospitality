using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.UserAccounts;

/// <summary>The browsers each account signed in from. One row per account and browser key (its SHA-256).</summary>
public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("UserDevices");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.KeyHash).IsRequired().IsFixedLength().HasMaxLength(UserDevice.KeyHashLength);
        builder.Property(e => e.Label).IsRequired().HasMaxLength(UserDevice.LabelMaxLength);
        builder.Property(e => e.LastAddress).HasMaxLength(UserDevice.AddressMaxLength);
        builder.Property(e => e.SessionId).HasMaxLength(UserDevice.SessionIdMaxLength);

        // A device belongs to its account; removing the device never touches the account.
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserAccountId, e.KeyHash }).IsUnique().HasDatabaseName("UX_UserDevices_Account_Key");
    }
}
