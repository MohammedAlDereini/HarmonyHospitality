namespace Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;

/// <summary>
/// Sent by the sign-in pages for an event that changes nothing else: a refused sign-in (wrong password or code, locked,
/// not active), a backup code used, two-step turned on in the setup page. Never bound from a request.
/// </summary>
public class RecordSecurityEventCommand : BaseCommandRequest<CallResponse>
{
    public Guid UserId { get; init; }

    public string TrailAction { get; init; } = null!;

    /// <summary>Words kept with the row, when the event has some: a reference, a reason.</summary>
    public string? Detail { get; init; }
}
