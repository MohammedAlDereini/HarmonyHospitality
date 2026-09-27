using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class TimeZoneRefConfiguration : IEntityTypeConfiguration<TimeZoneRef>
{
    public void Configure(EntityTypeBuilder<TimeZoneRef> builder)
    {
        builder.ToTable("TimeZones");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(e => e.IsActive).IsRequired();
    }
}