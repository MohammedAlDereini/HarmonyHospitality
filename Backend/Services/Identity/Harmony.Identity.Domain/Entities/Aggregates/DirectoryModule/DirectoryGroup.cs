using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;

/// <summary>
/// A group the company directory pushed (stage E, the ERD's DirectoryGroups), with its members. The group is the gate,
/// the Mews way: the customer's administrator gives the group one role, at one hotel, one hotel group or every hotel, and
/// its members hold exactly that. A group without a role gives nothing. The directory owns the name and the members; the
/// role is Harmony's.
/// </summary>
public sealed class DirectoryGroup : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int DisplayNameMaxLength = 256;

    public const int ExternalIdMaxLength = 256;

    private DirectoryGroup()
    {
    }

    private DirectoryGroup(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ProvisioningConnectionId { get; private set; }

    /// <summary>Unique inside the connection, as the directory spells it.</summary>
    public string DisplayName { get; private set; } = null!;

    /// <summary>The group's id in the directory (Entra's object id), when the directory sends one.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>The role the members hold; null: the group gives nothing.</summary>
    public Guid? RoleId { get; private set; }

    /// <summary>Where the role applies: one hotel, or (with PropertyGroupId) one hotel group; both null: every hotel.</summary>
    public Guid? PropertyId { get; private set; }

    public Guid? PropertyGroupId { get; private set; }

    /// <summary>Changed only through SetMembers / AddMembers / RemoveMembers.</summary>
    public ICollection<DirectoryGroupMember> Members { get; private set; } = new List<DirectoryGroupMember>();

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    /// <summary>The scope of the role; null with a role means every hotel of the customer (a tenant-wide role).</summary>
    public AssignmentScope? Scope => (PropertyId, PropertyGroupId) switch
    {
        ({ } hotel, _) => AssignmentScope.Hotel(hotel),
        (null, { } group) => AssignmentScope.Group(group),
        _ => null,
    };

    public static DirectoryGroup Create(Guid tenantId, Guid connectionId, string displayName, string? externalId)
    {
        var group = new DirectoryGroup(Guid.CreateVersion7()) { TenantId = tenantId, ProvisioningConnectionId = connectionId };
        group.Rename(displayName);
        group.Identify(externalId);
        return group;
    }

    public void Rename(string displayName)
    {
        var trimmed = displayName?.Trim();
        DisplayName = trimmed is { Length: > 0 and <= DisplayNameMaxLength }
            ? trimmed
            : throw new ArgumentException($"A group name of 1 to {DisplayNameMaxLength} characters is required.", nameof(displayName));
    }

    public void Identify(string? externalId)
    {
        var trimmed = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();
        ExternalId = trimmed is { Length: > ExternalIdMaxLength }
            ? throw new ArgumentException($"An external id is at most {ExternalIdMaxLength} characters.", nameof(externalId))
            : trimmed;
    }

    /// <summary>The role the members hold, and where; no role takes the scope away too.</summary>
    public void MapTo(Guid? roleId, AssignmentScope? scope)
    {
        if (roleId == Guid.Empty || (roleId is null && scope is not null))
        {
            throw new ArgumentException("A hotel or a hotel group needs a role.", nameof(roleId));
        }

        RoleId = roleId;
        PropertyId = scope?.PropertyId;
        PropertyGroupId = scope?.PropertyGroupId;
    }

    /// <summary>Exactly these members afterwards. Returns every account that joined or left.</summary>
    public IReadOnlyList<Guid> SetMembers(IEnumerable<Guid> userIds)
    {
        var wanted = userIds.Distinct().ToHashSet();
        var left = Members.Where(m => !wanted.Contains(m.UserAccountId)).ToList();
        foreach (var member in left)
        {
            Members.Remove(member);
        }

        return left.Select(m => m.UserAccountId).Concat(AddMembers(wanted)).ToList();
    }

    /// <summary>Returns the accounts that were not members yet.</summary>
    public IReadOnlyList<Guid> AddMembers(IEnumerable<Guid> userIds)
    {
        var joined = userIds.Distinct().Where(id => id != Guid.Empty && Members.All(m => m.UserAccountId != id)).ToList();
        foreach (var id in joined)
        {
            Members.Add(DirectoryGroupMember.Of(TenantId, id));
        }

        return joined;
    }

    /// <summary>Returns the accounts that were members.</summary>
    public IReadOnlyList<Guid> RemoveMembers(IEnumerable<Guid> userIds)
    {
        var leaving = userIds.ToHashSet();
        var left = Members.Where(m => leaving.Contains(m.UserAccountId)).ToList();
        foreach (var member in left)
        {
            Members.Remove(member);
        }

        return left.Select(m => m.UserAccountId).ToList();
    }
}

/// <summary>One account in one directory group. Owned by the group; the id is EF's.</summary>
public sealed class DirectoryGroupMember : BaseEntity<Guid>, IMultiTenantEntity
{
    private DirectoryGroupMember()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid DirectoryGroupId { get; private set; }

    public Guid UserAccountId { get; private set; }

    internal static DirectoryGroupMember Of(Guid tenantId, Guid userAccountId) => new() { TenantId = tenantId, UserAccountId = userAccountId };
}
