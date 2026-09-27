using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class CountrySubdivisionConfiguration : IEntityTypeConfiguration<CountrySubdivision>
{
    public void Configure(EntityTypeBuilder<CountrySubdivision> builder)
    {
        builder.ToTable("CountrySubdivisions");

        builder.HasKey(e => e.Code);

        builder.Property(e => e.Code).HasMaxLength(6);
        builder.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Kind).IsRequired().HasMaxLength(32);

        // Lets a city point at (country, subdivision) together, so its subdivision
        // can never belong to another country.
        builder.HasAlternateKey(e => new { e.CountryCode, e.Code });

        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(e => e.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}