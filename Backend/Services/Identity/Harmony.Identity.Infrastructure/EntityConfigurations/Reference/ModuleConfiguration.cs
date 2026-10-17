using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Reference;
public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.ToTable("Modules");

        builder.HasKey(e => e.Code);

        builder.Property(e => e.Code).HasMaxLength(64);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(e => e.OwnerService).IsRequired().HasMaxLength(64);

        // Stored as its name, like RegulatoryEnvironment.Kind, so the table reads like the console.
        builder.Property(e => e.Level).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.HasLimit).IsRequired();
    }
}