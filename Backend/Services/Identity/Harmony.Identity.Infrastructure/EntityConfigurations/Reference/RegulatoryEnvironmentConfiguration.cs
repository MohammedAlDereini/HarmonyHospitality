using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class RegulatoryEnvironmentConfiguration : IEntityTypeConfiguration<RegulatoryEnvironment>
{
    public void Configure(EntityTypeBuilder<RegulatoryEnvironment> builder)
    {
        builder.ToTable("RegulatoryEnvironments");

        builder.HasKey(e => e.Code);

        builder.Property(e => e.Code).HasMaxLength(32);
        builder.Property(e => e.Kind).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(e => e.OwnerService).IsRequired().HasMaxLength(64);

        builder.HasIndex(e => new { e.CountryCode, e.Kind });

        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(e => e.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}