using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class CapabilityConfiguration : IEntityTypeConfiguration<Capability>
{
    public void Configure(EntityTypeBuilder<Capability> builder)
    {
        builder.ToTable("Capabilities");

        builder.HasKey(e => e.Code);

        builder.Property(e => e.Code).HasMaxLength(64);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.OwnerService).IsRequired().HasMaxLength(64);
        builder.Property(e => e.HasLimit).IsRequired();
    }
}