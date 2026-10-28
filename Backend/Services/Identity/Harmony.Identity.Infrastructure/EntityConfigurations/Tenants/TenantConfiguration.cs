using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Tenants;

/// <summary>The Tenants table of the design: platform data, no TenantId column, the code unique across the installation.</summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", table =>
            table.HasCheckConstraint("CK_Tenants_InactivityDays", $"[InactivityDays] BETWEEN 1 AND {Tenant.MaxInactivityDays}"));

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(Tenant.CodeMaxLength);
        builder.Property(e => e.ExternalIdentifier).HasMaxLength(Tenant.ExternalIdentifierMaxLength);
        builder.Property(e => e.LegalNameEn).IsRequired().HasMaxLength(Tenant.LegalNameMaxLength);
        builder.Property(e => e.LegalNameAr).IsRequired().HasMaxLength(Tenant.LegalNameMaxLength);
        builder.Property(e => e.CountryCode).HasMaxLength(2);
        builder.Property(e => e.DefaultCurrency).HasMaxLength(3);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(e => e.StatusReason).HasMaxLength(500);
        builder.Property(e => e.Tier).IsRequired();
        builder.Property(e => e.OnboardedOn).IsRequired();
        builder.Property(e => e.InactivityDays).IsRequired().HasDefaultValue(Tenant.MaxInactivityDays);

        builder.Ignore(e => e.IsActive);

        builder.HasIndex(e => e.Code).IsUnique();

        // The reference rows a customer points at stay as long as the customer does.
        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(e => e.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CurrencyRef>()
            .WithMany()
            .HasForeignKey(e => e.DefaultCurrency)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
