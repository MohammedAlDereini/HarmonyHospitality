using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class CurrencyRefConfiguration : IEntityTypeConfiguration<CurrencyRef>
{
    public void Configure(EntityTypeBuilder<CurrencyRef> builder)
    {
        builder.ToTable("Currencies");

        builder.HasKey(e => e.Code);

        builder.Property(e => e.Code).HasMaxLength(3);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(e => e.MinorUnits).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
    }
}