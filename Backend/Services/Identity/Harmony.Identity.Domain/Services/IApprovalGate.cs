namespace Harmony.Identity.Domain.Services;

/// <summary>
/// Stage D's gate. A handler about to make a risky change asks it first: when the customer requires approvals, the change
/// is stored as a request for a second administrator and the handler stops with the gate's code (00131, or 00084 when
/// the maker has not confirmed it is them). When approvals are off, or the approval itself is running the change, the
/// handler goes on as before.
/// </summary>
public interface IApprovalGate
{
    /// <summary>
    /// Null: go on. Otherwise the code to answer with. <paramref name="guarded"/> are the accounts whose security version
    /// must not move before the approval; <paramref name="target"/> is the account the list shows.
    /// </summary>
    Task<string?> HoldAsync(string kind, object change, Guid? target, IReadOnlyCollection<Guid> guarded, CancellationToken cancellationToken);

    /// <summary>The approval that is running a change right now; null otherwise.</summary>
    Guid? Executing { get; }

    /// <summary>Marks the scope as running an approved request: the gate lets that change through.</summary>
    IDisposable Execute(Guid requestId);
}
