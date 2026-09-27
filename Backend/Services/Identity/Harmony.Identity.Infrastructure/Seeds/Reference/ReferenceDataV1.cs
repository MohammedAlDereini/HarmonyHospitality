using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Infrastructure.Seeds.Reference;

/// <summary>
/// The first set of reference rows: Jordan, the Gulf and Egypt. More countries come as new
/// seed versions (V2, V3...), never by editing this one, because it has already run in every database.
/// RegulatoryEnvironments is left empty on purpose: the Fiscal service owns its contents.
/// </summary>
public static class ReferenceDataV1
{
    public static IReadOnlyList<CurrencyRef> Currencies() =>
    [
        CurrencyRef.Create("JOD", "Jordanian Dinar", "دينار أردني"),
        CurrencyRef.Create("SAR", "Saudi Riyal", "ريال سعودي"),
        CurrencyRef.Create("AED", "UAE Dirham", "درهم إماراتي"),
        CurrencyRef.Create("KWD", "Kuwaiti Dinar", "دينار كويتي"),
        CurrencyRef.Create("QAR", "Qatari Riyal", "ريال قطري"),
        CurrencyRef.Create("BHD", "Bahraini Dinar", "دينار بحريني"),
        CurrencyRef.Create("OMR", "Omani Rial", "ريال عُماني"),
        CurrencyRef.Create("EGP", "Egyptian Pound", "جنيه مصري"),
        CurrencyRef.Create("USD", "US Dollar", "دولار أمريكي"),
        CurrencyRef.Create("EUR", "Euro", "يورو"),
        CurrencyRef.Create("GBP", "Pound Sterling", "جنيه إسترليني"),
    ];

    public static IReadOnlyList<TimeZoneRef> TimeZones() =>
    [
        TimeZoneRef.Create("Asia/Amman", "Amman", "عمّان"),
        TimeZoneRef.Create("Asia/Riyadh", "Riyadh", "الرياض"),
        TimeZoneRef.Create("Asia/Dubai", "Dubai", "دبي"),
        TimeZoneRef.Create("Asia/Kuwait", "Kuwait", "الكويت"),
        TimeZoneRef.Create("Asia/Qatar", "Qatar", "قطر"),
        TimeZoneRef.Create("Asia/Bahrain", "Bahrain", "البحرين"),
        TimeZoneRef.Create("Asia/Muscat", "Muscat", "مسقط"),
        TimeZoneRef.Create("Africa/Cairo", "Cairo", "القاهرة"),
    ];

    public static IReadOnlyList<Language> Languages() =>
    [
        Language.Create("ar", "Arabic", "العربية", isRightToLeft: true),
        Language.Create("en", "English", "English", isRightToLeft: false),
        Language.Create("fr", "French", "Français", isRightToLeft: false),
        Language.Create("de", "German", "Deutsch", isRightToLeft: false),
        Language.Create("es", "Spanish", "Español", isRightToLeft: false),
        Language.Create("it", "Italian", "Italiano", isRightToLeft: false),
        Language.Create("ru", "Russian", "Русский", isRightToLeft: false),
        Language.Create("tr", "Turkish", "Türkçe", isRightToLeft: false),
        Language.Create("zh", "Chinese", "中文", isRightToLeft: false),
    ];

    public static IReadOnlyList<Country> Countries() =>
    [
        Country.Create("JO", "JOR", "Jordan", "الأردن", "+962", "JOD"),
        Country.Create("SA", "SAU", "Saudi Arabia", "المملكة العربية السعودية", "+966", "SAR"),
        Country.Create("AE", "ARE", "United Arab Emirates", "الإمارات العربية المتحدة", "+971", "AED"),
        Country.Create("KW", "KWT", "Kuwait", "الكويت", "+965", "KWD"),
        Country.Create("QA", "QAT", "Qatar", "قطر", "+974", "QAR"),
        Country.Create("BH", "BHR", "Bahrain", "البحرين", "+973", "BHD"),
        Country.Create("OM", "OMN", "Oman", "عُمان", "+968", "OMR"),
        Country.Create("EG", "EGY", "Egypt", "مصر", "+20", "EGP"),
    ];

    public static IReadOnlyList<CountrySubdivision> Subdivisions() =>
    [
        CountrySubdivision.Create("JO", "JO-AJ", "Ajloun", "عجلون", "Governorate"),
        CountrySubdivision.Create("JO", "JO-AM", "Amman", "عمّان", "Governorate"),
        CountrySubdivision.Create("JO", "JO-AQ", "Aqaba", "العقبة", "Governorate"),
        CountrySubdivision.Create("JO", "JO-AT", "Tafilah", "الطفيلة", "Governorate"),
        CountrySubdivision.Create("JO", "JO-AZ", "Zarqa", "الزرقاء", "Governorate"),
        CountrySubdivision.Create("JO", "JO-BA", "Balqa", "البلقاء", "Governorate"),
        CountrySubdivision.Create("JO", "JO-IR", "Irbid", "إربد", "Governorate"),
        CountrySubdivision.Create("JO", "JO-JA", "Jerash", "جرش", "Governorate"),
        CountrySubdivision.Create("JO", "JO-KA", "Karak", "الكرك", "Governorate"),
        CountrySubdivision.Create("JO", "JO-MA", "Mafraq", "المفرق", "Governorate"),
        CountrySubdivision.Create("JO", "JO-MD", "Madaba", "مادبا", "Governorate"),
        CountrySubdivision.Create("JO", "JO-MN", "Ma'an", "معان", "Governorate"),

        CountrySubdivision.Create("SA", "SA-01", "Riyadh", "الرياض", "Region"),
        CountrySubdivision.Create("SA", "SA-02", "Makkah", "مكة المكرمة", "Region"),
        CountrySubdivision.Create("SA", "SA-03", "Madinah", "المدينة المنورة", "Region"),
        CountrySubdivision.Create("SA", "SA-04", "Eastern Province", "المنطقة الشرقية", "Region"),
        CountrySubdivision.Create("SA", "SA-05", "Al-Qassim", "القصيم", "Region"),
        CountrySubdivision.Create("SA", "SA-06", "Ha'il", "حائل", "Region"),
        CountrySubdivision.Create("SA", "SA-07", "Tabuk", "تبوك", "Region"),
        CountrySubdivision.Create("SA", "SA-08", "Northern Borders", "الحدود الشمالية", "Region"),
        CountrySubdivision.Create("SA", "SA-09", "Jazan", "جازان", "Region"),
        CountrySubdivision.Create("SA", "SA-10", "Najran", "نجران", "Region"),
        CountrySubdivision.Create("SA", "SA-11", "Al-Bahah", "الباحة", "Region"),
        CountrySubdivision.Create("SA", "SA-12", "Al-Jawf", "الجوف", "Region"),
        CountrySubdivision.Create("SA", "SA-14", "Asir", "عسير", "Region"),

        CountrySubdivision.Create("AE", "AE-AZ", "Abu Dhabi", "أبوظبي", "Emirate"),
        CountrySubdivision.Create("AE", "AE-AJ", "Ajman", "عجمان", "Emirate"),
        CountrySubdivision.Create("AE", "AE-DU", "Dubai", "دبي", "Emirate"),
        CountrySubdivision.Create("AE", "AE-FU", "Fujairah", "الفجيرة", "Emirate"),
        CountrySubdivision.Create("AE", "AE-RK", "Ras Al Khaimah", "رأس الخيمة", "Emirate"),
        CountrySubdivision.Create("AE", "AE-SH", "Sharjah", "الشارقة", "Emirate"),
        CountrySubdivision.Create("AE", "AE-UQ", "Umm Al Quwain", "أم القيوين", "Emirate"),
    ];

    // Coordinates to two decimals (about a kilometre): enough for a city, and honest about precision.
    public static IReadOnlyList<City> Cities() =>
    [
        City.Create("JO", "JO-AM", "Amman", "عمّان", "Asia/Amman", 31.95m, 35.93m),
        City.Create("JO", "JO-AZ", "Zarqa", "الزرقاء", "Asia/Amman", 32.07m, 36.09m),
        City.Create("JO", "JO-IR", "Irbid", "إربد", "Asia/Amman", 32.56m, 35.85m),
        City.Create("JO", "JO-AQ", "Aqaba", "العقبة", "Asia/Amman", 29.53m, 35.01m),
        City.Create("JO", "JO-MD", "Madaba", "مادبا", "Asia/Amman", 31.72m, 35.79m),
        City.Create("JO", "JO-MN", "Wadi Musa", "وادي موسى", "Asia/Amman", 30.32m, 35.48m),
        City.Create("JO", "JO-MN", "Ma'an", "معان", "Asia/Amman", 30.19m, 35.73m),
        City.Create("JO", "JO-JA", "Jerash", "جرش", "Asia/Amman", 32.28m, 35.90m),
        City.Create("JO", "JO-AJ", "Ajloun", "عجلون", "Asia/Amman", 32.33m, 35.75m),
        City.Create("JO", "JO-KA", "Karak", "الكرك", "Asia/Amman", 31.18m, 35.70m),
        City.Create("JO", "JO-BA", "Salt", "السلط", "Asia/Amman", 32.04m, 35.73m),
        City.Create("JO", "JO-MA", "Mafraq", "المفرق", "Asia/Amman", 32.34m, 36.21m),
        City.Create("JO", "JO-AT", "Tafilah", "الطفيلة", "Asia/Amman", 30.84m, 35.60m),

        City.Create("SA", "SA-01", "Riyadh", "الرياض", "Asia/Riyadh", 24.71m, 46.68m),
        City.Create("SA", "SA-02", "Jeddah", "جدة", "Asia/Riyadh", 21.49m, 39.19m),
        City.Create("SA", "SA-02", "Makkah", "مكة المكرمة", "Asia/Riyadh", 21.39m, 39.86m),
        City.Create("SA", "SA-03", "Madinah", "المدينة المنورة", "Asia/Riyadh", 24.47m, 39.61m),
        City.Create("SA", "SA-03", "AlUla", "العلا", "Asia/Riyadh", 26.62m, 37.92m),
        City.Create("SA", "SA-04", "Dammam", "الدمام", "Asia/Riyadh", 26.43m, 50.10m),
        City.Create("SA", "SA-04", "Al Khobar", "الخبر", "Asia/Riyadh", 26.28m, 50.21m),

        City.Create("AE", "AE-DU", "Dubai", "دبي", "Asia/Dubai", 25.20m, 55.27m),
        City.Create("AE", "AE-AZ", "Abu Dhabi", "أبوظبي", "Asia/Dubai", 24.45m, 54.38m),
        City.Create("AE", "AE-SH", "Sharjah", "الشارقة", "Asia/Dubai", 25.35m, 55.40m),
        City.Create("AE", "AE-RK", "Ras Al Khaimah", "رأس الخيمة", "Asia/Dubai", 25.79m, 55.94m),

        City.Create("KW", null, "Kuwait City", "مدينة الكويت", "Asia/Kuwait", 29.38m, 47.99m),
        City.Create("QA", null, "Doha", "الدوحة", "Asia/Qatar", 25.29m, 51.53m),
        City.Create("BH", null, "Manama", "المنامة", "Asia/Bahrain", 26.23m, 50.59m),
        City.Create("OM", null, "Muscat", "مسقط", "Asia/Muscat", 23.59m, 58.41m),
        City.Create("EG", null, "Cairo", "القاهرة", "Africa/Cairo", 30.04m, 31.24m),
    ];

    public static IReadOnlyList<PropertyTypeRef> PropertyTypes() =>
    [
        PropertyTypeRef.Create("Hotel", "Hotel", "فندق", 1),
        PropertyTypeRef.Create("Hostel", "Hostel", "نُزُل", 2),
        PropertyTypeRef.Create("Resort", "Resort", "منتجع", 3),
        PropertyTypeRef.Create("Villas", "Villas", "فلل", 4),
        PropertyTypeRef.Create("Apartment", "Serviced apartments", "شقق فندقية", 5),
    ];

    public static IReadOnlyList<PropertyGroupTypeRef> PropertyGroupTypes() =>
    [
        PropertyGroupTypeRef.Create("Brand", "Brand", "علامة تجارية", isExclusive: true, isSystem: true),
        PropertyGroupTypeRef.Create("Region", "Region", "منطقة", isExclusive: false, isSystem: true),
        PropertyGroupTypeRef.Create("Owner", "Owner", "مالك", isExclusive: false, isSystem: true),
        PropertyGroupTypeRef.Create("Custom", "Custom", "مخصصة", isExclusive: false, isSystem: true),
    ];

    public static IReadOnlyList<Capability> Capabilities() =>
    [
        Capability.Create("pms.core", "PMS core", "Identity", hasLimit: true),
        Capability.Create("multi.property", "Multiple properties", "Identity", hasLimit: true),
    ];
}
