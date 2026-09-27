using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class PropertyTypeRefConfiguration : IEntityTypeConfiguration<PropertyTypeRef>
{
    public void Configure(EntityTypeBuilder<PropertyTypeRef> builder)
    {
        builder.ToTable("PropertyTypes");

        builder.HasKey(e => e.Code);

        builder.Property(e => e.Code).HasMaxLength(32);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(e => e.DisplayOrder).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
    }
}