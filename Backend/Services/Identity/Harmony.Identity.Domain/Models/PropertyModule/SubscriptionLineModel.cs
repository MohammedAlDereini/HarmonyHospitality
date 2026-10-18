namespace Harmony.Identity.Domain.Models.PropertyModule;

/// <summary>One line of a customer's subscription as the console sees it. IsHeld is computed for today, never stored.</summary>
public class SubscriptionLineModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Null = chain-wide.</summary>
    public Guid? PropertyId { get; set; }

    public string ModuleCode { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public string? DisabledReason { get; set; }

    public bool RenewsAutomatically { get; set; }

    public int? Rooms { get; set; }

    public int? Terminals { get; set; }

    public DateOnly StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }

    /// <summary>Enabled, started, and ended by the calendar only without auto-renewal; today as a UTC day.</summary>
    public bool IsHeld { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
