using System.Security.Cryptography;
using System.Text;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.EnrolmentModule;

/// <summary>Where an invitation stands. Expiry is a time, not a state: a Pending row past ExpiresAtUtc is expired.</summary>
public enum EnrolmentState
{
    Pending = 1,
    Redeemed = 2,
    Cancelled = 3,
}

/// <summary>
/// An invitation to Harmony's own sign-in (design 3.3, the ERD's LocalStaffEnrolments): who sent it, for which account,
/// until when. The invitee's e-mail is kept only as a digest, never in plain text. An expired or cancelled invitation can
/// never set a first password. Company login people need none: they sign in with their company.
/// </summary>
public sealed class LocalStaffEnrolment : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    /// <summary>How long an invitation link lives (Identity's own token lifetime is the same 24 hours).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>Hex SHA-256: 64 characters.</summary>
    public const int DigestLength = 64;

    private LocalStaffEnrolment()
    {
    }

    private LocalStaffEnrolment(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>The person who sent the invitation.</summary>
    public Guid SponsorUserAccountId { get; private set; }

    /// <summary>The customer's local authority: where the invitation is redeemed.</summary>
    public Guid FederatedAuthorityId { get; private set; }

    /// <summary>The account the invitation is for (not in the ERD; added so the administrator can resend or cancel it).</summary>
    public Guid UserAccountId { get; private set; }

    /// <summary>SHA-256 of the invitee's normalised e-mail, hex: enough to recognise the address, never to read it.</summary>
    public string InviteeHandleDigest { get; private set; } = null!;

    public EnrolmentState State { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsOpen(DateTimeOffset now) => State == EnrolmentState.Pending && now < ExpiresAtUtc;

    public static LocalStaffEnrolment Invite(Guid tenantId, Guid sponsorUserAccountId, Guid localAuthorityId, Guid userAccountId, string email, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || sponsorUserAccountId == Guid.Empty || localAuthorityId == Guid.Empty || userAccountId == Guid.Empty)
        {
            throw new ArgumentException("A tenant, a sponsor, the local authority and an account are required.");
        }

        return new LocalStaffEnrolment(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            SponsorUserAccountId = sponsorUserAccountId,
            FederatedAuthorityId = localAuthorityId,
            UserAccountId = userAccountId,
            InviteeHandleDigest = DigestOf(email),
            State = EnrolmentState.Pending,
            ExpiresAtUtc = now.ToUniversalTime().Add(Lifetime),
        };
    }

    /// <summary>The first password was set through this invitation. Only an open invitation can be redeemed.</summary>
    public void Redeem(DateTimeOffset now)
    {
        if (!IsOpen(now))
        {
            throw new InvalidOperationException("Only an open invitation can be redeemed.");
        }

        State = EnrolmentState.Redeemed;
    }

    /// <summary>Withdrawn (or replaced by a new one). Only a pending invitation changes; anything else stays as it is.</summary>
    public void Cancel()
    {
        if (State == EnrolmentState.Pending)
        {
            State = EnrolmentState.Cancelled;
        }
    }

    /// <summary>The digest of an e-mail: trimmed, upper-cased invariantly (Identity's normalisation), SHA-256, hex.</summary>
    public static string DigestOf(string email)
    {
        var normal = (email ?? string.Empty).Trim().ToUpperInvariant();
        if (normal.Length == 0)
        {
            throw new ArgumentException("An e-mail is required.", nameof(email));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normal))).ToLowerInvariant();
    }
}
