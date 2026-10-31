namespace Harmony.Identity.Domain.Models.PropertyModule;

/// <summary>The hotels in numbers, for the console's home (GET /api/Property/summary).</summary>
public class PropertySummaryModel
{
    /// <summary>The UTC day the numbers were taken on.</summary>
    public DateOnly Today { get; set; }

    public int Total { get; set; }

    public int Active { get; set; }

    /// <summary>Hotels whose opening day is still ahead, within ninety days.</summary>
    public int OpeningSoon { get; set; }

    /// <summary>Hotels created in the current month.</summary>
    public int CreatedThisMonth { get; set; }

    /// <summary>Rooms of the active hotels, as they declare them.</summary>
    public int TotalRooms { get; set; }

    /// <summary>Rooms licensed today across the active hotels: each hotel's largest held line, added up.</summary>
    public int LicensedRooms { get; set; }

    /// <summary>Active hotels running more rooms than they license.</summary>
    public int AboveLicence { get; set; }

    /// <summary>Those hotels, largest gap first, five at most.</summary>
    public List<PropertyLicenceModel> AboveLicenceProperties { get; set; } = [];
}

/// <summary>A hotel named for its licence gap.</summary>
public class PropertyLicenceModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public int TotalRooms { get; set; }

    public int LicensedRooms { get; set; }
}
