namespace Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;

/// <summary>A group a hotel belongs to at a moment, as the settings resolver reads it: who, how strong, switched on or off.</summary>
public sealed record HotelGroup(Guid Id, string Code, int Precedence, bool IsActive);
