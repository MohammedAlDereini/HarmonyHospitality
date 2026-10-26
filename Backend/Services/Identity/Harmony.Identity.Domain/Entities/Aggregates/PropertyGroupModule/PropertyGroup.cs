using System.Text.RegularExpressions;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.DomainEvents.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;

/// <summary>
/// Any named set of a customer's hotels: a brand, a region, an owner, a custom grouping (design 1.3, the ERD's
/// PropertyGroups). Inside the customer's tenant (the wall); made and kept by the platform, read by the hotel's own
/// people. Which hotels are in it, and since when, is the membership aggregate's business, not this one's.
/// Fixed once saved: the code and the type. The member rows copy the type and a composite key holds the copies
/// honest, so the type never changes; to re-type a group, end its memberships and make a new one.
/// </summary>
public sealed partial class PropertyGroup : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 128;
    public const int DescriptionMaxLength = 500;
    public const int ExternalIdentifierMaxLength = 64;

    private PropertyGroup()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>A row of PropertyGroupTypes (Brand, Region, Owner...). Fixed once saved.</summary>
    public string TypeCode { get; private set; } = null!;

    /// <summary>The short name we and the customer call it by, like RITZ-CARLTON. Chosen once; upper-case; unique in the tenant.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>The group's reference in the system that sends the bill. Optional, ours to read.</summary>
    public string? ExternalIdentifier { get; private set; }

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>Higher wins a standards tie.</summary>
    public Precedence Precedence { get; private set; } = null!;

    public int DisplayOrder { get; private set; }

    /// <summary>An inactive group keeps its history; no hotel joins it.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The brand's own columns; only when the type is Brand, else null. Changed only through SetBrandDetail.</summary>
    public BrandDetail? Brand { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    /// <summary>
    /// A new group of a customer. The tenant is set here, explicitly: the context never stamps it. Raises
    /// PropertyGroupCreatedDomainEvent, published by the framework after the save commits.
    /// </summary>
    public static PropertyGroup Create(
        Guid tenantId,
        string typeCode,
        string code,
        string nameEn,
        string nameAr,
        string? description,
        Precedence precedence,
        int displayOrder,
        string? externalIdentifier)
    {
        ArgumentNullException.ThrowIfNull(precedence);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant is required.", nameof(tenantId));
        }

        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayOrder), displayOrder, "Display order is zero or more.");
        }

        var group = new PropertyGroup
        {
            TenantId = tenantId,
            TypeCode = ReferenceText.Key(typeCode, PropertyGroupTypeRef.CodeMaxLength, nameof(typeCode)),
            Code = GroupCode(code),
            NameEn = Name(nameEn, nameof(nameEn)),
            NameAr = Name(nameAr, nameof(nameAr)),
            Description = Optional(description, DescriptionMaxLength, nameof(description)),
            Precedence = precedence,
            DisplayOrder = displayOrder,
            ExternalIdentifier = Optional(externalIdentifier, ExternalIdentifierMaxLength, nameof(externalIdentifier)),
            IsActive = true,
        };

        group.Raise(new PropertyGroupCreatedDomainEvent(group));
        return group;
    }

    /// <summary>What may change: the names, the words, the precedence, the order, the reference, and whether it is active. Never the code or the type.</summary>
    public void Update(string nameEn, string nameAr, string? description, Precedence precedence, int displayOrder, string? externalIdentifier, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(precedence);
        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayOrder), displayOrder, "Display order is zero or more.");
        }

        NameEn = Name(nameEn, nameof(nameEn));
        NameAr = Name(nameAr, nameof(nameAr));
        Description = Optional(description, DescriptionMaxLength, nameof(description));
        Precedence = precedence;
        DisplayOrder = displayOrder;
        ExternalIdentifier = Optional(externalIdentifier, ExternalIdentifierMaxLength, nameof(externalIdentifier));
        IsActive = isActive;
    }

    /// <summary>The type the schema names: brand detail and the one-brand-per-hotel index apply.</summary>
    public bool IsBrand => string.Equals(TypeCode, PropertyGroupTypeRef.BrandCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The brand detail, replaced whole: null clears it (the row goes). Only a Brand group carries one; the handler
    /// refuses the rest first (00098), this is the last guard.
    /// </summary>
    public void SetBrandDetail(BrandDetail.Request? request)
    {
        if (request is null)
        {
            Brand = null;
            return;
        }

        if (!IsBrand)
        {
            throw new InvalidOperationException($"Group {Code} is a {TypeCode}, not a brand: it carries no brand detail.");
        }

        if (Brand is null)
        {
            Brand = BrandDetail.New(this, request);
        }
        else
        {
            Brand.Update(request);
        }
    }

    private static string GroupCode(string? value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!CodePattern().IsMatch(code))
        {
            throw new ArgumentException($"'{value}' is not a group code: 2 to {CodeMaxLength} characters of letters, digits, . - _", nameof(value));
        }

        return code;
    }

    private static string Name(string? value, string argument)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is 0 or > NameMaxLength)
        {
            throw new ArgumentException($"A name of 1 to {NameMaxLength} characters is required.", argument);
        }

        return name;
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

    [GeneratedRegex("^[A-Z0-9._-]{2,32}$")]
    private static partial Regex CodePattern();
}
