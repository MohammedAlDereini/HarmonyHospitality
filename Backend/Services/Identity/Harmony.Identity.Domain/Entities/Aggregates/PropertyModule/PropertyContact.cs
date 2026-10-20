using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Common;

namespace Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;

/// <summary>
/// A named person at the hotel (general manager, finance, IT), for us and the customer to reach. Owned by the hotel,
/// born and changed only through it; inside the wall like its hotel. One primary at most, kept by the hotel.
/// </summary>
public sealed class PropertyContact : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int NameMaxLength = 128;
    public const int RoleMaxLength = 64;
    public const int EmailMaxLength = 256;
    public const int TelephoneMaxLength = 32;

    private PropertyContact()
    {
    }

    private PropertyContact(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    public Guid PropertyId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>What the person does at the hotel, in words: "General manager", "Finance".</summary>
    public string? Role { get; private set; }

    public string? Email { get; private set; }

    public string? Telephone { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    internal static PropertyContact New(Guid tenantId, Guid propertyId, string name, string? role, string? email, string? telephone, bool isPrimary)
    {
        // The id is given at birth, so one list can be reconciled against itself before any save.
        var contact = new PropertyContact(Guid.CreateVersion7()) { TenantId = tenantId, PropertyId = propertyId };
        contact.Update(name, role, email, telephone, isPrimary);
        return contact;
    }

    internal void Update(string name, string? role, string? email, string? telephone, bool isPrimary)
    {
        var text = name?.Trim() ?? string.Empty;
        if (text.Length is 0 or > NameMaxLength)
        {
            throw new ArgumentException($"A name of 1 to {NameMaxLength} characters is required.", nameof(name));
        }

        Name = text;
        Role = Optional(role, RoleMaxLength, nameof(role));
        Email = Optional(email, EmailMaxLength, nameof(email));
        Telephone = Optional(telephone, TelephoneMaxLength, nameof(telephone));
        IsPrimary = isPrimary;
    }

    private static string? Optional(string? value, int maxLength, string argument)
    {
        var text = IdentityText.Blank(value);
        if (text is { Length: var length } && length > maxLength)
        {
            throw new ArgumentException($"At most {maxLength} characters.", argument);
        }

        return text;
    }
}
