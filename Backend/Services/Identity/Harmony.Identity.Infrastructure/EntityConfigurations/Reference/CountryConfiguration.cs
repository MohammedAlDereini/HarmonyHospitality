using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");

        builder.HasKey(e => e.Code);

        builder.Property(e => e.Code).HasMaxLength(2);
        builder.Property(e => e.Alpha3).IsRequired().HasMaxLength(3);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(e => e.DialCode).IsRequired().HasMaxLength(5);
        builder.Property(e => e.DefaultCurrency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Alpha3).IsUnique();

        builder.HasOne<CurrencyRef>()
            .WithMany()
            .HasForeignKey(e => e.DefaultCurrency)
            .OnDelete(DeleteBehavior.Restrict);
    }
}