using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Aggregates.LookupModule;

public class LookupValue : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, ISoftDeleteEntity
{
    public Guid LookupCategoryId { get; private set; }

    public LookupCategory Category { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsGlobal { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsDeleted { get; set; }

    public static LookupValue New(Guid lookupCategoryId, string name, string? description, bool isActive = true)
    {
        return new LookupValue
        {
            LookupCategoryId = lookupCategoryId,
            Name = name,
            NormalizedName = LookupCategory.Normalize(name),
            Description = description,
            IsActive = isActive,
            IsGlobal = false,
        };
    }

    public static LookupValue NewGlobal(Guid lookupCategoryId, string name, string? description, bool isActive = true)
    {
        return new LookupValue
        {
            LookupCategoryId = lookupCategoryId,
            Name = name,
            NormalizedName = LookupCategory.Normalize(name),
            Description = description,
            IsActive = isActive,
            IsGlobal = true,
        };
    }

    public void Update(string name, string? description, bool isActive)
    {
        this.Name = name;
        this.NormalizedName = LookupCategory.Normalize(name);
        this.Description = description;
        this.IsActive = isActive;
    }

    public void Delete()
    {
        this.IsDeleted = true;
    }
}