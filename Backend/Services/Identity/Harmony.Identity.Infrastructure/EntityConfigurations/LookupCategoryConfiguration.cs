using Harmony.Identity.Domain.Aggregates.LookupModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations;

public class LookupCategoryConfiguration : IEntityTypeConfiguration<LookupCategory>
{
    public void Configure(EntityTypeBuilder<LookupCategory> builder)
    {
        builder.ToTable("LookupCategories");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.NormalizedName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.IsGlobal)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(e => new { e.NormalizedName, e.TenantId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}