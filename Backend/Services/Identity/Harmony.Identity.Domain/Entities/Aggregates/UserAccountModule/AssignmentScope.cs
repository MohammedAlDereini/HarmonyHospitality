namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// Where a role applies: one hotel, or one group (every hotel in it, today and tomorrow). Never both, never neither
/// (design 2.2): the only constructors are the two factories, and the table carries the same CHECK.
/// </summary>
public sealed record AssignmentScope
{
    private AssignmentScope(Guid? propertyId, Guid? propertyGroupId)
    {
        PropertyId = propertyId;
        PropertyGroupId = propertyGroupId;
    }

    public Guid? PropertyId { get; }

    public Guid? PropertyGroupId { get; }

    public bool IsHotel => PropertyId is not null;

    public static AssignmentScope Hotel(Guid propertyId)
        => propertyId == Guid.Empty ? throw new ArgumentException("A hotel is required.", nameof(propertyId)) : new(propertyId, null);

    public static AssignmentScope Group(Guid propertyGroupId)
        => propertyGroupId == Guid.Empty ? throw new ArgumentException("A group is required.", nameof(propertyGroupId)) : new(null, propertyGroupId);

    /// <summary>From a door's two optional ids: exactly one must be given (the validator says 10327 first).</summary>
    public static AssignmentScope Of(Guid? propertyId, Guid? propertyGroupId)
        => (propertyId, propertyGroupId) switch
        {
            ({ } hotel, null) => Hotel(hotel),
            (null, { } group) => Group(group),
            _ => throw new ArgumentException("Exactly one of a hotel and a group.", nameof(propertyId)),
        };
}
