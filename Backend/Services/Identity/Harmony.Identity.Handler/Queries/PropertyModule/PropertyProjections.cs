using System.Linq.Expressions;
using Harmony.Core.Identity.Subscriptions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Models.PropertyModule;

namespace Harmony.Identity.Handler.Queries.PropertyModule;

internal static class PropertyProjections
{
    /// <summary>The hotel row as the console sees it; the owned value objects read as flat columns.</summary>
    public static readonly Expression<Func<Property, PropertyModel>> Model = e => new PropertyModel
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Code = e.Code,
        ExternalIdentifier = e.ExternalIdentifier,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        PropertyTypeCode = e.PropertyTypeCode,
        CityId = e.Location.CityId,
        CountryCode = e.Location.CountryCode,
        SubdivisionCode = e.Location.SubdivisionCode,
        TimeZoneId = e.Location.TimeZoneId,
        AddressLine = e.Address.Line,
        PostalCode = e.Address.PostalCode,
        DefaultCurrency = e.DefaultCurrency,
        DefaultLanguageCode = e.DefaultLanguageCode,
        TotalRooms = e.TotalRooms,
        LegalEnvironmentCode = e.Regulatory.Legal,
        TaxEnvironmentCode = e.Regulatory.Tax,
        AccountingEnvironmentCode = e.Regulatory.Accounting,
        AccommodationEnvironmentCode = e.Regulatory.Accommodation,
        OpensOn = e.OpensOn,
        ClosesOn = e.ClosesOn,
        IsActive = e.IsActive,
        ShortName = e.ShortName,
        UniqueName = e.UniqueName,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        BusinessId = e.BusinessId,
        BusinessRegistrationCode = e.BusinessRegistrationCode,
        BusinessPremiseId = e.BusinessPremiseId,
        FiscalYearBeginMonth = e.FiscalYearBeginMonth,
        FiscalYearBeginDay = e.FiscalYearBeginDay,
        DefaultCulture = e.DefaultCulture,
        StarRating = e.StarRating,
        Email = e.Email,
        Telephone = e.Telephone,
        WebsiteUrl = e.WebsiteUrl,
        ProductChampionEmail = e.ProductChampionEmail,
        LegalOwnerName = e.LegalOwnerName,
        DisplayOrder = e.DisplayOrder,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };

    /// <summary>A line as stored; IsHeld is filled afterwards, for today.</summary>
    public static readonly Expression<Func<TenantSubscription, SubscriptionLineModel>> Line = s => new SubscriptionLineModel
    {
        Id = s.Id,
        TenantId = s.TenantId,
        PropertyId = s.PropertyId,
        ModuleCode = s.ModuleCode,
        IsEnabled = s.IsEnabled,
        DisabledReason = s.DisabledReason,
        RenewsAutomatically = s.RenewsAutomatically,
        Rooms = s.Rooms,
        Terminals = s.Terminals,
        StartsOn = s.StartsOn,
        EndsOn = s.EndsOn,
        CreatedDate = s.CreatedDate,
        ModifiedDate = s.ModifiedDate,
    };

    /// <summary>IsHeld by the one definition every service uses (the framework record), for the given UTC day.</summary>
    public static void MarkHeld(IEnumerable<SubscriptionLineModel> lines, DateOnly utcToday)
    {
        foreach (var line in lines)
        {
            line.IsHeld = new SubscriptionLine(line.PropertyId, line.ModuleCode, line.IsEnabled, line.RenewsAutomatically, line.Rooms, line.Terminals, line.StartsOn, line.EndsOn).IsHeldOn(utcToday);
        }
    }

    /// <summary>The computed facts of the detail: open today (the hotel's local day), licensed rooms, the gap.</summary>
    public static void Complete(PropertyModel model, List<SubscriptionLineModel> lines, DateTimeOffset utcNow)
    {
        var utcToday = DateOnly.FromDateTime(utcNow.UtcDateTime);
        MarkHeld(lines, utcToday);
        model.Subscriptions = lines;
        model.LicensedRooms = lines.Where(l => l.IsHeld && l.Rooms is not null).Max(l => l.Rooms);
        model.RoomsAboveLicence = model.LicensedRooms is { } max && model.TotalRooms > max;

        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(model.TimeZoneId)).DateTime);
        model.IsOpenToday = model.IsActive && (model.OpensOn is null || localToday >= model.OpensOn) && (model.ClosesOn is null || localToday <= model.ClosesOn);
    }
}
