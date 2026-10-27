namespace Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>What the resolver could say about a key at a hotel.</summary>
public enum ResolutionOutcome
{
    /// <summary>A value won: the hotel's, a group's, the customer's or the system default.</summary>
    Value = 1,

    /// <summary>Nobody set anything and the setting has no default.</summary>
    Unset = 2,

    /// <summary>Two or more groups at the same highest precedence set it: an error, never a coin toss.</summary>
    Conflict = 3,

    /// <summary>The setting is narrowed to other property types than this hotel's.</summary>
    NotApplicable = 4,
}

/// <summary>How one place fared in the resolution.</summary>
public enum ResolutionVerdict
{
    /// <summary>This place's value is the answer.</summary>
    Won = 1,

    /// <summary>Set here, but a place above won.</summary>
    Overridden = 2,

    /// <summary>Nothing set here.</summary>
    NotSet = 3,

    /// <summary>Set here, at the same highest precedence as another group: nobody wins.</summary>
    Tied = 4,

    /// <summary>A group that sets it, outranked by a group of higher precedence.</summary>
    Outranked = 5,

    /// <summary>The group is switched off: its values do not count.</summary>
    Inactive = 6,
}

/// <summary>Where a step of the trace looked.</summary>
public enum ResolutionLevel
{
    Property = 1,
    PropertyGroup = 2,
    Tenant = 3,
    SystemDefault = 4,
}

/// <summary>One line of the trace: a place (the hotel's or the group's id; none for the customer and the default), what it holds for the key, and how it fared.</summary>
public sealed record ResolutionStep(
    ResolutionLevel Level,
    Guid? Id,
    string? Code,
    int? Precedence,
    string? Value,
    ResolutionVerdict Verdict);

/// <summary>
/// The answer and how it was reached: every place that was looked at, in order, with what it held and how it fared.
/// The trace is the feature (design 5.1): a person sees why a hotel checks in at 15:00, not just that it does.
/// </summary>
public sealed record Resolution(
    ResolutionOutcome Outcome,
    string? Value,
    ResolutionLevel? Source,
    string? SourceCode,
    IReadOnlyList<ResolutionStep> Trace);

/// <summary>The hotel being resolved for: its code and its property type.</summary>
public sealed record HotelFacts(Guid Id, string Code, string PropertyTypeCode);

/// <summary>A group the hotel is in at the moment of resolving, and what it set for the key (null when nothing).</summary>
public sealed record GroupStanding(Guid Id, string Code, int Precedence, bool IsActive, string? Value);
