namespace Harmony.Identity.Domain.Models.SecurityAuditModule;

/// <summary>
/// One line of the platform's trail across every customer (POST /api/SecurityAudit/get-all), as the console's Audit log
/// shows it: the row itself plus the names it needs read beside it. Never a secret, never an e-mail.
/// </summary>
public class PlatformTrailEventModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>The customer's code and names; null when the tenant row is gone.</summary>
    public string? TenantCode { get; set; }

    public string? TenantNameEn { get; set; }

    public string? TenantNameAr { get; set; }

    /// <summary>The account the event is about.</summary>
    public Guid UserAccountId { get; set; }

    public string? UserDisplayName { get; set; }

    public string Action { get; set; } = null!;

    /// <summary>Who did it: an account id, the account itself when it acted alone, or "system".</summary>
    public string ActorSubject { get; set; } = null!;

    /// <summary>The actor's name when the actor is an account of ours; null for "system" or an id no longer known.</summary>
    public string? ActorDisplayName { get; set; }

    public string? SourceAddress { get; set; }

    public string? Device { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>One short fact about the action, as the trail keeps it.</summary>
    public string? Detail { get; set; }
}
