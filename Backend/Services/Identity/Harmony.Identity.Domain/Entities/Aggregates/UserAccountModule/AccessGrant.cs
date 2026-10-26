using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Common;

namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// A temporary elevation: this account acts as this role at one hotel or group between two moments, because someone
/// with the right to grant said so, for a reason. Owned by the account. Never silent: who, why and until when are
/// columns, and the trail carries the grant and the revocation.
/// </summary>
public sealed class AccessGrant : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int ReasonMaxLength = 500;
    public static readonly TimeSpan LongestWindow = TimeSpan.FromDays(30);

    private AccessGrant()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public AssignmentScope Scope { get; private set; } = null!;

    public DateTimeOffset FromUtc { get; private set; }

    public DateTimeOffset ToUtc { get; private set; }

    public Guid GrantedById { get; private set; }

    public string Reason { get; private set; } = null!;

    /// <summary>Set when revoked before ToUtc; the window then ends here.</summary>
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsActiveAt(DateTimeOffset utcNow) => utcNow >= FromUtc && utcNow < ToUtc && (RevokedAtUtc is null || utcNow < RevokedAtUtc);

    public bool HasEndedAt(DateTimeOffset utcNow) => RevokedAtUtc is not null || utcNow >= ToUtc;

    internal static AccessGrant New(Guid tenantId, Guid roleId, AssignmentScope scope, DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid grantedById, string? reason)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var from = fromUtc.ToUniversalTime();
        var to = toUtc.ToUniversalTime();
        if (to <= from || to - from > LongestWindow)
        {
            throw new ArgumentException($"A grant runs from a moment to a later moment, at most {LongestWindow.TotalDays} days.", nameof(toUtc));
        }

        var why = IdentityText.Blank(reason) ?? throw new ArgumentException("A reason is required.", nameof(reason));
        if (why.Length > ReasonMaxLength)
        {
            throw new ArgumentException($"A reason is at most {ReasonMaxLength} characters.", nameof(reason));
        }

        return new AccessGrant { TenantId = tenantId, RoleId = roleId, Scope = scope, FromUtc = from, ToUtc = to, GrantedById = grantedById, Reason = why };
    }

    internal void Revoke(DateTimeOffset atUtc)
    {
        if (HasEndedAt(atUtc))
        {
            throw new InvalidOperationException("The grant has already ended.");
        }

        RevokedAtUtc = atUtc.ToUniversalTime();
    }
}
