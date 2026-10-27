using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.IdentityLinkModule;

/// <summary>
/// A company identity bound to a Harmony account (design 3.2, the ERD's ExternalIdentityLinks): the company login that
/// vouches for it and the provider's permanent id for the person there (Microsoft's object id, Google's subject). A
/// company sign-in finds the account through an open link, never through the e-mail. The periods are its history.
/// </summary>
public sealed class ExternalIdentityLink : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int SubjectMaxLength = 256;
    public const int LinkedByMaxLength = 64;

    /// <summary>Bound on the person's first company sign-in, by their work e-mail inside their own company.</summary>
    public const string FirstSignIn = "first-sign-in";

    private ExternalIdentityLink()
    {
    }

    private ExternalIdentityLink(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>The account the link is bound to now (the open period's), or was last bound to.</summary>
    public Guid UserAccountId { get; private set; }

    public Guid FederatedAuthorityId { get; private set; }

    /// <summary>The provider's permanent id for the person: Microsoft's oid, Google's sub. Unique per company login.</summary>
    public string ProviderSubject { get; private set; } = null!;

    /// <summary>How the link was made: "first-sign-in", or the account id of the administrator who made it.</summary>
    public string LinkedBy { get; private set; } = null!;

    public ICollection<IdentityLinkPeriod> Periods { get; private set; } = new List<IdentityLinkPeriod>();

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsOpen => Periods.Any(p => p.IsOpen);

    /// <summary>A company identity bound to an account from now on.</summary>
    public static ExternalIdentityLink Bind(Guid tenantId, Guid authorityId, string providerSubject, Guid userAccountId, string linkedBy, DateTimeOffset at)
    {
        if (tenantId == Guid.Empty || authorityId == Guid.Empty || userAccountId == Guid.Empty)
        {
            throw new ArgumentException("A tenant, a company login and an account are required.");
        }

        var link = new ExternalIdentityLink(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            FederatedAuthorityId = authorityId,
            ProviderSubject = Text(providerSubject, SubjectMaxLength, nameof(providerSubject)),
            UserAccountId = userAccountId,
            LinkedBy = Text(linkedBy, LinkedByMaxLength, nameof(linkedBy)),
        };

        link.Periods.Add(IdentityLinkPeriod.Open(tenantId, link.Id, userAccountId, at));
        return link;
    }

    /// <summary>The binding ends now: the open period closes. Ending an ended link changes nothing.</summary>
    public void End(DateTimeOffset at)
    {
        foreach (var period in Periods.Where(p => p.IsOpen))
        {
            period.Close(at);
        }
    }

    /// <summary>Bound again from now on, to the same account or another: the open period (if any) closes first.</summary>
    public void Rebind(Guid userAccountId, string linkedBy, DateTimeOffset at)
    {
        if (userAccountId == Guid.Empty)
        {
            throw new ArgumentException("An account is required.", nameof(userAccountId));
        }

        End(at);
        UserAccountId = userAccountId;
        LinkedBy = Text(linkedBy, LinkedByMaxLength, nameof(linkedBy));
        Periods.Add(IdentityLinkPeriod.Open(TenantId, Id, userAccountId, at));
    }

    private static string Text(string? value, int maxLength, string argument)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is 0 || text.Length > maxLength)
        {
            throw new ArgumentException($"1 to {maxLength} characters are required.", argument);
        }

        return text;
    }
}
