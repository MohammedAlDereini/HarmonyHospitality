using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class PropertyGroupTypeRefConfiguration : IEntityTypeConfiguration<PropertyGroupTypeRef>
{
    public void Configure(EntityTypeBuilder<PropertyGroupTypeRef> builder)
    {
        builder.ToTable("PropertyGroupTypes");

        builder.HasKey(e => e.Code);

        builder.Property(e => e.Code).HasMaxLength(32);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(e => e.IsExclusive).IsRequired();
        builder.Property(e => e.IsSystem).IsRequired();
    }
}