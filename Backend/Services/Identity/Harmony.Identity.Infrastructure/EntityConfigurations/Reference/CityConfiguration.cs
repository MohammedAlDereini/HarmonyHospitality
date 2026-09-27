using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(e => e.SubdivisionCode).HasMaxLength(6);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(e => e.TimeZoneId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Latitude).HasPrecision(9, 6);
        builder.Property(e => e.Longitude).HasPrecision(9, 6);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => new { e.CountryCode, e.NameEn }).IsUnique();

        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(e => e.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CountrySubdivision>()
            .WithMany()
            .HasForeignKey(e => new { e.CountryCode, e.SubdivisionCode })
            .HasPrincipalKey(s => new { s.CountryCode, s.Code })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TimeZoneRef>()
            .WithMany()
            .HasForeignKey(e => e.TimeZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}