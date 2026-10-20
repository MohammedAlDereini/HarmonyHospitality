using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Properties;

/// <summary>
/// The Properties table of the design. A multi-tenant row (the framework adds the tenant filter and the TenantId index);
/// the code unique per customer; the three value objects owned and flattened into the same row under the ERD's column
/// names; every reference code a real foreign key that refuses deletion while a hotel points at it. The domain events
/// are not columns.
/// </summary>
public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(Property.CodeMaxLength);
        builder.Property(e => e.ExternalIdentifier).HasMaxLength(Property.ExternalIdentifierMaxLength);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(Property.NameMaxLength);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(Property.NameMaxLength);
        builder.Property(e => e.PropertyTypeCode).IsRequired().HasMaxLength(PropertyTypeRef.CodeMaxLength);
        builder.Property(e => e.DefaultCurrency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.DefaultLanguageCode).IsRequired().HasMaxLength(2);
        builder.Property(e => e.TotalRooms).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        // Where the hotel stands: the city decides country, subdivision and time zone. Flat columns, real keys.
        builder.OwnsOne(e => e.Location, location =>
        {
            location.Property(l => l.CityId).HasColumnName("CityId").IsRequired();
            location.Property(l => l.CountryCode).HasColumnName("CountryCode").IsRequired().HasMaxLength(2);
            location.Property(l => l.SubdivisionCode).HasColumnName("SubdivisionCode").HasMaxLength(CountrySubdivision.CodeMaxLength);
            location.Property(l => l.TimeZoneId).HasColumnName("TimeZoneId").IsRequired().HasMaxLength(64);
            location.HasOne<City>().WithMany().HasForeignKey(l => l.CityId).OnDelete(DeleteBehavior.Restrict);
            location.HasOne<Country>().WithMany().HasForeignKey(l => l.CountryCode).OnDelete(DeleteBehavior.Restrict);
            location.HasOne<CountrySubdivision>().WithMany().HasForeignKey(l => l.SubdivisionCode).OnDelete(DeleteBehavior.Restrict);
            location.HasOne<TimeZoneRef>().WithMany().HasForeignKey(l => l.TimeZoneId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.OwnsOne(e => e.Address, address =>
        {
            address.Property(a => a.Line).HasColumnName("AddressLine").IsRequired().HasMaxLength(PostalAddress.LineMaxLength);
            address.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(PostalAddress.PostalCodeMaxLength);
        });

        // The four regimes: one column each, each a real key into RegulatoryEnvironments.
        builder.OwnsOne(e => e.Regulatory, regulatory =>
        {
            regulatory.Property(r => r.Legal).HasColumnName("LegalEnvironmentCode").IsRequired().HasMaxLength(RegulatoryEnvironment.CodeMaxLength);
            regulatory.Property(r => r.Tax).HasColumnName("TaxEnvironmentCode").IsRequired().HasMaxLength(RegulatoryEnvironment.CodeMaxLength);
            regulatory.Property(r => r.Accounting).HasColumnName("AccountingEnvironmentCode").IsRequired().HasMaxLength(RegulatoryEnvironment.CodeMaxLength);
            regulatory.Property(r => r.Accommodation).HasColumnName("AccommodationEnvironmentCode").IsRequired().HasMaxLength(RegulatoryEnvironment.CodeMaxLength);
            regulatory.HasOne<RegulatoryEnvironment>().WithMany().HasForeignKey(r => r.Legal).OnDelete(DeleteBehavior.Restrict);
            regulatory.HasOne<RegulatoryEnvironment>().WithMany().HasForeignKey(r => r.Tax).OnDelete(DeleteBehavior.Restrict);
            regulatory.HasOne<RegulatoryEnvironment>().WithMany().HasForeignKey(r => r.Accounting).OnDelete(DeleteBehavior.Restrict);
            regulatory.HasOne<RegulatoryEnvironment>().WithMany().HasForeignKey(r => r.Accommodation).OnDelete(DeleteBehavior.Restrict);
        });

        // The hotel's own details (step 3): widths now, doors later.
        builder.Property(e => e.ShortName).HasMaxLength(Property.ShortNameMaxLength);
        builder.Property(e => e.UniqueName).HasMaxLength(Property.UniqueNameMaxLength);
        builder.Property(e => e.Latitude).HasPrecision(9, 6);
        builder.Property(e => e.Longitude).HasPrecision(9, 6);
        builder.Property(e => e.BusinessId).HasMaxLength(Property.BusinessTextMaxLength);
        builder.Property(e => e.BusinessRegistrationCode).HasMaxLength(Property.BusinessTextMaxLength);
        builder.Property(e => e.BusinessPremiseId).HasMaxLength(Property.BusinessTextMaxLength);
        builder.Property(e => e.DefaultCulture).HasMaxLength(Property.DefaultCultureMaxLength);
        builder.Property(e => e.Email).HasMaxLength(Property.EmailMaxLength);
        builder.Property(e => e.Telephone).HasMaxLength(Property.TelephoneMaxLength);
        builder.Property(e => e.WebsiteUrl).HasMaxLength(Property.WebsiteUrlMaxLength);
        builder.Property(e => e.ProductChampionEmail).HasMaxLength(Property.EmailMaxLength);
        builder.Property(e => e.LegalOwnerName).HasMaxLength(Property.LegalOwnerNameMaxLength);

        // One code per customer. The handler asks first; this is the answer when two console users race.
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("UX_Properties_TenantId_Code");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PropertyTypeRef>().WithMany().HasForeignKey(e => e.PropertyTypeCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CurrencyRef>().WithMany().HasForeignKey(e => e.DefaultCurrency).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Language>().WithMany().HasForeignKey(e => e.DefaultLanguageCode).OnDelete(DeleteBehavior.Restrict);
    }
}
