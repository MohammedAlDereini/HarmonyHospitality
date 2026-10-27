using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Config;

/// <summary>
/// The ConfigSettings table of the design: global, the key the primary key (the column is named Key, as the ERD says).
/// The data type is held to its five words by a CHECK as well as by the domain. The domain events are not columns.
/// </summary>
public class ConfigSettingConfiguration : IEntityTypeConfiguration<ConfigSetting>
{
    public void Configure(EntityTypeBuilder<ConfigSetting> builder)
    {
        builder.ToTable("ConfigSettings", table =>
            table.HasCheckConstraint("CK_ConfigSettings_DataType", "[DataType] IN (N'time', N'int', N'bool', N'decimal', N'string')"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("Key").HasMaxLength(ConfigSetting.KeyMaxLength);
        builder.Ignore(e => e.Key);
        builder.Ignore(e => e.TypeCodes);
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.OwnerService).IsRequired().HasMaxLength(ConfigSetting.OwnerServiceMaxLength);
        builder.Property(e => e.DataType).IsRequired().HasMaxLength(ConfigSetting.DataTypeMaxLength);
        builder.Property(e => e.SystemDefault).HasMaxLength(ConfigSetting.DefaultMaxLength);
        builder.Property(e => e.AppliesToTypes).HasMaxLength(ConfigSetting.AppliesToTypesMaxLength);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.OwnerService).HasDatabaseName("IX_ConfigSettings_OwnerService");
    }
}
