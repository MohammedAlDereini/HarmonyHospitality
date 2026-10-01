namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// What an administrator decided about the account. Terminated is final.
/// </summary>
public enum AdministrativeState
{
    Active = 1,
    Suspended = 2,
    Terminated = 3,
}