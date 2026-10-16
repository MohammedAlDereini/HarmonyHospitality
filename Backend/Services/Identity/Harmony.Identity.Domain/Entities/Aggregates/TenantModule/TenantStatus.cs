namespace Harmony.Identity.Domain.Entities.Aggregates.TenantModule;

/// <summary>
/// Where a customer stands with us. Active: its people sign in and work. Suspended: nobody in it signs in and nothing
/// in it changes, until the platform reinstates it (an unpaid bill, a dispute). Terminated: final, the contract ended;
/// the rows stay for the export window and the books, the people never sign in again.
/// </summary>
public enum TenantStatus
{
    Active = 1,
    Suspended = 2,
    Terminated = 3,
}
