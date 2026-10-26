namespace Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;

/// <summary>
/// Since when, and until when, a hotel is in a group. Open-ended while ToUtc is null. Dated, so a February invoice
/// still resolves to February's brand after a March rebrand. Always UTC.
/// </summary>
public sealed record MembershipPeriod
{
    private MembershipPeriod(DateTimeOffset fromUtc, DateTimeOffset? toUtc)
    {
        FromUtc = fromUtc;
        ToUtc = toUtc;
    }

    public DateTimeOffset FromUtc { get; }

    /// <summary>Null means current.</summary>
    public DateTimeOffset? ToUtc { get; }

    public bool IsCurrent => ToUtc is null;

    public static MembershipPeriod From(DateTimeOffset fromUtc) => new(fromUtc.ToUniversalTime(), null);

    /// <summary>The same period, closed. Never before it began.</summary>
    public MembershipPeriod Ended(DateTimeOffset toUtc)
    {
        var end = toUtc.ToUniversalTime();
        if (end < FromUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc), toUtc, "A membership cannot end before it began.");
        }

        return new MembershipPeriod(FromUtc, end);
    }

    public bool Covers(DateTimeOffset at) => at >= FromUtc && (ToUtc is null || at < ToUtc);
}
