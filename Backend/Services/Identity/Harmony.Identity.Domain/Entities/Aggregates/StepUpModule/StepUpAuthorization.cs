using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.StepUpModule;

/// <summary>
/// "Confirm it is you" (design 3.5, the ERD's StepUpAuthorizations): the person proved themselves again just now, with a
/// password, a passkey, or their company's sign-in. Minutes, not hours: the expiry is set at creation and never extended;
/// if it could be extended it would be a session, and the whole point is that it is not.
/// </summary>
public sealed class StepUpAuthorization : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    /// <summary>Long enough to finish the one thing that asked for it (add a passkey, raise a request).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public const int MethodMaxLength = 16;

    private StepUpAuthorization()
    {
    }

    private StepUpAuthorization(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    public Guid OperatorUserAccountId { get; private set; }

    /// <summary>Where the proof was made: the customer's local authority, or its company login.</summary>
    public Guid FederatedAuthorityId { get; private set; }

    /// <summary>password, passkey or company.</summary>
    public string Method { get; private set; } = null!;

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsLive(DateTimeOffset now) => now < ExpiresAtUtc;

    public static StepUpAuthorization Prove(Guid tenantId, Guid operatorUserAccountId, Guid authorityId, string method, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || operatorUserAccountId == Guid.Empty || authorityId == Guid.Empty)
        {
            throw new ArgumentException("A tenant, the person and the authority are required.");
        }

        var how = method?.Trim().ToLowerInvariant() ?? string.Empty;
        if (how is not ("password" or "passkey" or "company"))
        {
            throw new ArgumentException("The proof is a password, a passkey or the company's sign-in.", nameof(method));
        }

        return new StepUpAuthorization(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            OperatorUserAccountId = operatorUserAccountId,
            FederatedAuthorityId = authorityId,
            Method = how,
            ExpiresAtUtc = now.ToUniversalTime().Add(Lifetime),
        };
    }
}
