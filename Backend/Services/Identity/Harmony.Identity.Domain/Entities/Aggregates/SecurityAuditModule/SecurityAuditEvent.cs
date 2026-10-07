using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;

/// <summary>
/// One line of the security trail (design docs, wave four, SecurityAuditModule): something happened to an account, who did it,
/// from where, when. Append-only and deliberately dumb: there is no method to change or delete a row. Action stays free text
/// (see <see cref="SecurityAuditActions"/>) so a newer service version can always write its own.
/// </summary>
public sealed class SecurityAuditEvent : BaseEntity<Guid>, IMultiTenantEntity
{
    public const int ActionMaxLength = 64;
    public const int ActorMaxLength = 64;
    public const int AddressMaxLength = 45;
    public const int DeviceMaxLength = 64;

    private SecurityAuditEvent()
    {
    }

    private SecurityAuditEvent(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>The account the event is about.</summary>
    public Guid UserAccountId { get; private set; }

    public string Action { get; private set; } = null!;

    /// <summary>Who did it: an account id, the account itself when it acted alone (sign-in, reset link), or "system".</summary>
    public string ActorSubject { get; private set; } = null!;

    /// <summary>The IP address the request came from, when there was a request.</summary>
    public string? SourceAddress { get; private set; }

    /// <summary>"Chrome on Windows": read from the browser's User-Agent, never the raw header.</summary>
    public string? Device { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static SecurityAuditEvent Record(Guid tenantId, Guid userAccountId, string action, string actorSubject, string? sourceAddress, string? device, DateTimeOffset occurredAtUtc)
    {
        if (userAccountId == Guid.Empty)
        {
            throw new ArgumentException("The account is required.", nameof(userAccountId));
        }

        return new SecurityAuditEvent(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            UserAccountId = userAccountId,
            Action = Required(action, ActionMaxLength, nameof(action)),
            ActorSubject = Required(actorSubject, ActorMaxLength, nameof(actorSubject)),
            SourceAddress = Cut(sourceAddress, AddressMaxLength),
            Device = Cut(device, DeviceMaxLength),
            OccurredAtUtc = occurredAtUtc,
        };
    }

    // Our code passes these, never a request: an empty or oversized one is a bug.
    private static string Required(string value, int max, string name)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= max ? value : throw new ArgumentException($"1 to {max} characters required.", name);

    private static string? Cut(string? value, int max)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
