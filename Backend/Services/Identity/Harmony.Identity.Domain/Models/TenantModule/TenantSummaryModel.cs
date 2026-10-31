namespace Harmony.Identity.Domain.Models.TenantModule;

/// <summary>The customers in numbers, for the console's home (GET /api/Tenant/summary). The platform's own row is never counted.</summary>
public class TenantSummaryModel
{
    /// <summary>The UTC day the numbers were taken on.</summary>
    public DateOnly Today { get; set; }

    public int Active { get; set; }

    public int Suspended { get; set; }

    public int Terminated { get; set; }

    /// <summary>Distinct countries of the active customers.</summary>
    public int Countries { get; set; }

    /// <summary>Customers onboarded in the current month.</summary>
    public int OnboardedThisMonth { get; set; }

    /// <summary>The last twelve months, oldest first, the current month last; a month with no customer is a zero.</summary>
    public List<MonthCountModel> OnboardedByMonth { get; set; } = [];

    /// <summary>The suspended customers, most recently changed first, five at most.</summary>
    public List<TenantBriefModel> SuspendedTenants { get; set; } = [];

    /// <summary>Fixed-term lines still on that end within thirty days, soonest first, ten at most.</summary>
    public List<EndingLineModel> LinesEndingSoon { get; set; } = [];
}

/// <summary>One month of the chart: "2026-10" and a count.</summary>
public class MonthCountModel
{
    public string Month { get; set; } = null!;

    public int Count { get; set; }
}

/// <summary>A customer named in the summary: enough to show and to open.</summary>
public class TenantBriefModel
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string LegalNameEn { get; set; } = null!;

    public string LegalNameAr { get; set; } = null!;

    public string? StatusReason { get; set; }

    /// <summary>When the row last changed (the suspension, normally).</summary>
    public DateTime? ModifiedDate { get; set; }
}

/// <summary>A subscription line that ends soon: whose, where, which module, when.</summary>
public class EndingLineModel
{
    public Guid TenantId { get; set; }

    public string TenantCode { get; set; } = null!;

    public string TenantNameEn { get; set; } = null!;

    public string TenantNameAr { get; set; } = null!;

    /// <summary>The hotel the line is for; null for a chain-wide line.</summary>
    public Guid? PropertyId { get; set; }

    public string? PropertyCode { get; set; }

    public string ModuleCode { get; set; } = null!;

    public DateOnly EndsOn { get; set; }

    public int? Rooms { get; set; }
}
