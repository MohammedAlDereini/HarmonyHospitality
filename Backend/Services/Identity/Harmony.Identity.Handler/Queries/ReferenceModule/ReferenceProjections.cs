using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule;

/// <summary>Entity to model, once per table, so the list and the detail of a table can never disagree on a column.</summary>
public static class ReferenceProjections
{
    public static readonly Expression<Func<Country, CountryModel>> Country = e => new CountryModel
    {
        Code = e.Code,
        Alpha3 = e.Alpha3,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        DialCode = e.DialCode,
        DefaultCurrency = e.DefaultCurrency,
        IsActive = e.IsActive,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    public static readonly Expression<Func<CurrencyRef, CurrencyModel>> Currency = e => new CurrencyModel
    {
        Code = e.Code,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        MinorUnits = e.MinorUnits,
        IsActive = e.IsActive,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    public static readonly Expression<Func<TimeZoneRef, TimeZoneModel>> TimeZone = e => new TimeZoneModel
    {
        Id = e.Id,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        IsActive = e.IsActive,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    public static readonly Expression<Func<Language, LanguageModel>> Language = e => new LanguageModel
    {
        Code = e.Code,
        NameEn = e.NameEn,
        NameNative = e.NameNative,
        IsRightToLeft = e.IsRightToLeft,
        IsActive = e.IsActive,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    public static readonly Expression<Func<CountrySubdivision, CountrySubdivisionModel>> CountrySubdivision = e => new CountrySubdivisionModel
    {
        Code = e.Code,
        CountryCode = e.CountryCode,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        Kind = e.Kind,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    public static readonly Expression<Func<City, CityModel>> City = e => new CityModel
    {
        Id = e.Id,
        CountryCode = e.CountryCode,
        SubdivisionCode = e.SubdivisionCode,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        TimeZoneId = e.TimeZoneId,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        IsActive = e.IsActive,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    public static readonly Expression<Func<PropertyTypeRef, PropertyTypeModel>> PropertyType = e => new PropertyTypeModel
    {
        Code = e.Code,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        DisplayOrder = e.DisplayOrder,
        IsActive = e.IsActive,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    public static readonly Expression<Func<PropertyGroupTypeRef, PropertyGroupTypeModel>> PropertyGroupType = e => new PropertyGroupTypeModel
    {
        Code = e.Code,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        IsExclusive = e.IsExclusive,
        IsSystem = e.IsSystem,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    public static readonly Expression<Func<Capability, CapabilityModel>> Capability = e => new CapabilityModel
    {
        Code = e.Code,
        NameEn = e.NameEn,
        OwnerService = e.OwnerService,
        HasLimit = e.HasLimit,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    public static readonly Expression<Func<RegulatoryEnvironment, RegulatoryEnvironmentModel>> RegulatoryEnvironment = e => new RegulatoryEnvironmentModel
    {
        Code = e.Code,
        Kind = e.Kind,
        CountryCode = e.CountryCode,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        OwnerService = e.OwnerService,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };
}
