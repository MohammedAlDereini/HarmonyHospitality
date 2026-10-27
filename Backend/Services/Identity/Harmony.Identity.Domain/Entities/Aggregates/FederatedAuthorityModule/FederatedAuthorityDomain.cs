using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;

/// <summary>
/// One e-mail domain a company login covers (ritz.com). Owned by its authority, born and removed only through it.
/// A domain belongs to one customer on the whole platform: the unique index holds that in SQL.
/// </summary>
public sealed class FederatedAuthorityDomain : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int DomainMaxLength = 253;

    private FederatedAuthorityDomain()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid FederatedAuthorityId { get; private set; }

    /// <summary>Lower case, no "@": the part of an e-mail address after the @.</summary>
    public string Domain { get; private set; } = null!;

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    internal static FederatedAuthorityDomain New(Guid tenantId, Guid authorityId, string domain)
    {
        // The id is EF's, assigned when the save tracks the row: a child with a key already set would be taken for an existing one.
        return new FederatedAuthorityDomain { TenantId = tenantId, FederatedAuthorityId = authorityId, Domain = domain };
    }
}
