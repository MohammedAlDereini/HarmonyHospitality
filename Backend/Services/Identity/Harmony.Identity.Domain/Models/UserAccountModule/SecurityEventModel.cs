namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>One line of an account's security trail, as the API shows it.</summary>
public class SecurityEventModel
{
    public string Action { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Who did it: an account id; the account's own id when it acted itself.</summary>
    public string ActorSubject { get; set; } = null!;

    public string? SourceAddress { get; set; }
    public string? Device { get; set; }

    /// <summary>One short fact about the action: which passkey signed in, how the person proved themselves.</summary>
    public string? Detail { get; set; }
}
