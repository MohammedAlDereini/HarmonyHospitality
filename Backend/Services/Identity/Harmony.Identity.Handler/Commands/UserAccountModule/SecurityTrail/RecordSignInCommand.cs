namespace Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;

/// <summary>
/// Sent by the sign-in pages once a person is signed in: which account, how (the trail action), which browser (its device
/// key, from the device cookie) and which sign-in session opened there. Never bound from a request.
/// </summary>
public class RecordSignInCommand : BaseCommandRequest<CallResponse>
{
    public Guid UserId { get; init; }

    public string TrailAction { get; init; } = null!;

    public string DeviceKey { get; init; } = null!;

    public string? SessionId { get; init; }

    /// <summary>One short fact for the trail line: which passkey signed in. Null for the other methods.</summary>
    public string? Detail { get; init; }
}
