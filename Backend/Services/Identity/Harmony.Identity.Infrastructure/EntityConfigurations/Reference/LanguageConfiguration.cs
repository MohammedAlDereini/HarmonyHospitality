using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("Languages");

        builder.HasKey(e => e.Code);

        builder.Property(e => e.Code).HasMaxLength(2);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.NameNative).IsRequired().HasMaxLength(128);
        builder.Property(e => e.IsRightToLeft).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
    }
}