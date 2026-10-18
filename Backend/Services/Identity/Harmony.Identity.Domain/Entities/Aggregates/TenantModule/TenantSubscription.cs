using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Entities.Aggregates.TenantModule;

/// <summary>
/// One line of the customer's contract: a hotel's module with its licensed counts, or a chain-wide module
/// (PropertyId null). Owned by the Tenant, one line per hotel and module, born and changed only through it.
/// Platform data like its parent, so NOT a multi-tenant row: TenantId names the customer, it is not the wall column.
/// The calendar ends a line only when the customer gave notice (RenewsAutomatically false): a paying hotel is never
/// switched off by a date nobody renewed. Mirrors Harmony.Core.Identity.Subscriptions.SubscriptionLine, which is what
/// the services read.
/// </summary>
public sealed class TenantSubscription : BaseEntity<Guid>, IAuditableEntity, IConcurrentEntity
{
    private TenantSubscription()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>The hotel this line is for; null = chain-wide (loyalty, multi-property).</summary>
    public Guid? PropertyId { get; private set; }

    /// <summary>A row of Modules, stored in the module's own spelling.</summary>
    public string ModuleCode { get; private set; } = null!;

    public bool IsEnabled { get; private set; }

    /// <summary>Why we switched it off, for the console. Null while enabled.</summary>
    public string? DisabledReason { get; private set; }

    /// <summary>True unless the customer gave notice: then EndsOn is a real end.</summary>
    public bool RenewsAutomatically { get; private set; }

    /// <summary>Rooms licensed at the hotel; hotel lines only. The hotel's real count may differ: that gap is flagged, never refused.</summary>
    public int? Rooms { get; private set; }

    /// <summary>Terminals (tills) licensed at the hotel; hotel lines only.</summary>
    public int? Terminals { get; private set; }

    public DateOnly StartsOn { get; private set; }

    /// <summary>End of the current term. Null = open-ended.</summary>
    public DateOnly? EndsOn { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    /// <summary>The one question: enabled, started, and ended by the calendar only without auto-renewal.</summary>
    public bool IsHeldOn(DateOnly day)
        => IsEnabled && day >= StartsOn && (RenewsAutomatically || EndsOn is null || day <= EndsOn);

    internal static TenantSubscription New(Guid? propertyId, string moduleCode, int? rooms, int? terminals, DateOnly startsOn, DateOnly? endsOn, bool renewsAutomatically)
    {
        var line = new TenantSubscription
        {
            PropertyId = propertyId,
            ModuleCode = ReferenceText.Key(moduleCode, Module.CodeMaxLength, nameof(moduleCode)),
        };
        line.Renew(rooms, terminals, startsOn, endsOn, renewsAutomatically);
        return line;
    }

    /// <summary>A later subscribe: new counts, term and renewal, and switched back on if it was off.</summary>
    internal void Renew(int? rooms, int? terminals, DateOnly startsOn, DateOnly? endsOn, bool renewsAutomatically)
    {
        if (PropertyId is null && (rooms is not null || terminals is not null))
        {
            throw new ArgumentException("A chain-wide line carries no rooms or terminals.");
        }

        // No invented ceilings: a hotel has as many rooms and tills as it has. Only "none" is refused.
        if (rooms is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rooms), rooms, "Rooms are at least 1.");
        }

        if (terminals is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(terminals), terminals, "Terminals are at least 1.");
        }

        if (endsOn is { } last && last < startsOn)
        {
            throw new ArgumentException("The term ends before it starts.", nameof(endsOn));
        }

        Rooms = rooms;
        Terminals = terminals;
        StartsOn = startsOn;
        EndsOn = endsOn;
        RenewsAutomatically = renewsAutomatically;
        IsEnabled = true;
        DisabledReason = null;
    }

    /// <summary>Switched off with a reason; the row stays for the history. Step 2's door.</summary>
    internal void Disable(string reason)
    {
        IsEnabled = false;
        DisabledReason = IdentityText.Reason(reason) ?? throw new ArgumentException("A reason is required.", nameof(reason));
    }

    internal bool Is(Guid? propertyId, string? moduleCode)
        => PropertyId == propertyId && string.Equals(ModuleCode, moduleCode?.Trim(), StringComparison.OrdinalIgnoreCase);
}
