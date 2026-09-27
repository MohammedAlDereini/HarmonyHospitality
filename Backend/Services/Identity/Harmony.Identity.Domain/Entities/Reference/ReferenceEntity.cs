using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>
/// Base for the seeded reference tables: shared by every tenant, so no TenantId,
/// but audited and versioned like every other row.
/// </summary>
public abstract class ReferenceEntity : IAuditableEntity, IConcurrentEntity
{
    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;
}