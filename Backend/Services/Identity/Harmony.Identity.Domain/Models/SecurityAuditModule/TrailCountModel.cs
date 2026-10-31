namespace Harmony.Identity.Domain.Models.SecurityAuditModule;

/// <summary>How many times one action was written to the trail inside a window (GET /api/SecurityAudit/counts).</summary>
public class TrailCountModel
{
    public string Action { get; set; } = null!;

    public int Count { get; set; }
}
