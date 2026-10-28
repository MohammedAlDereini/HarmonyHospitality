namespace Harmony.Identity.Domain.Services;

/// <summary>
/// Stage E's inactivity rule, one round: suspends the accounts that have not signed in for their customer's number of
/// days and returns how many. A nightly job runs it; the platform may run it now.
/// </summary>
public interface IInactivitySuspension
{
    Task<int> RunAsync(CancellationToken cancellationToken);
}
