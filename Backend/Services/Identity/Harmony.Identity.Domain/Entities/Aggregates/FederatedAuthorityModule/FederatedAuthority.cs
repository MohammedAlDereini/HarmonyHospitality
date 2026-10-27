using System.Text.RegularExpressions;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;

/// <summary>
/// One way a customer's people prove who they are (design 3.1, the ERD's FederatedAuthorities). Every customer has its
/// "local" authority, Harmony's own sign-in. A company login (Microsoft or Google) is added by the platform; the
/// customer's administrator lists the company's e-mail domains and switches it on. While it is on, a person of the
/// customer whose e-mail is on one of those domains signs in with the company, unless the administrator kept them on a
/// Harmony password (UserAccounts.PasswordSignIn).
/// </summary>
public sealed partial class FederatedAuthority : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    /// <summary>The issuer of Harmony's own sign-in, the same word in every customer and every environment.</summary>
    public const string LocalIssuer = "local";

    public const int IssuerMaxLength = 256;
    public const int AudienceMaxLength = 256;

    /// <summary>
    /// Mail domains anybody can sign up at. One of them on a list would send everybody's address there to one customer's
    /// company login (Mews refuses them too).
    /// </summary>
    private static readonly HashSet<string> PublicMailDomains = new(StringComparer.Ordinal)
    {
        "gmail.com", "googlemail.com", "outlook.com", "hotmail.com", "live.com", "msn.com",
        "yahoo.com", "icloud.com", "me.com", "aol.com", "proton.me", "protonmail.com",
    };

    private FederatedAuthority()
    {
    }

    private FederatedAuthority(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    public AuthorityScheme Scheme { get; private set; }

    /// <summary>
    /// Who signs the company's tokens, exactly as the provider writes it: https://login.microsoftonline.com/{tid}/v2.0 for
    /// Microsoft (the company's own tenant id inside), https://accounts.google.com for Google, "local" for Harmony.
    /// </summary>
    public string Issuer { get; private set; } = null!;

    /// <summary>The audience the company's tokens must carry: Harmony's app id at that provider. Null for the local one.</summary>
    public string? Audience { get; private set; }

    public bool IsEnabled { get; private set; }

    /// <summary>The e-mail domains this company login covers. Changed only through AddDomain / RemoveDomain.</summary>
    public ICollection<FederatedAuthorityDomain> Domains { get; private set; } = new List<FederatedAuthorityDomain>();

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsCompany => Scheme != AuthorityScheme.Local;

    /// <summary>Harmony's own sign-in for a customer: born with the customer, always on.</summary>
    public static FederatedAuthority Local(Guid tenantId)
    {
        RequireTenant(tenantId);
        return new FederatedAuthority(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            Scheme = AuthorityScheme.Local,
            Issuer = LocalIssuer,
            IsEnabled = true,
        };
    }

    /// <summary>A company login, added by the platform. Off until the customer's administrator lists its domains and switches it on.</summary>
    public static FederatedAuthority Company(Guid tenantId, AuthorityScheme scheme, string issuer, string audience)
    {
        RequireTenant(tenantId);
        if (scheme == AuthorityScheme.Local)
        {
            throw new ArgumentException("A company login is Microsoft or Google.", nameof(scheme));
        }

        return new FederatedAuthority(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            Scheme = scheme,
            Issuer = Required(issuer, IssuerMaxLength, nameof(issuer)),
            Audience = Required(audience, AudienceMaxLength, nameof(audience)),
            IsEnabled = false,
        };
    }

    /// <summary>Adds an e-mail domain, stored in DomainName's form. False when it is on the list already.</summary>
    public bool AddDomain(string domain)
    {
        RequireCompany();
        var name = DomainName(domain);
        if (Domains.Any(d => d.Domain == name))
        {
            return false;
        }

        Domains.Add(FederatedAuthorityDomain.New(TenantId, Id, name));
        return true;
    }

    /// <summary>Removes an e-mail domain. False when it is not on the list. The last one cannot go while the login is on.</summary>
    public bool RemoveDomain(string domain)
    {
        RequireCompany();
        var name = DomainName(domain);
        var row = Domains.FirstOrDefault(d => d.Domain == name);
        if (row is null)
        {
            return false;
        }

        if (IsEnabled && Domains.Count == 1)
        {
            throw new InvalidOperationException("Switch the company login off before removing its last domain.");
        }

        Domains.Remove(row);
        return true;
    }

    /// <summary>On: the listed domains sign in with the company. Needs a domain; the handler checks the fallback administrator.</summary>
    public void SwitchOn()
    {
        RequireCompany();
        if (Domains.Count == 0)
        {
            throw new InvalidOperationException("A company login needs an e-mail domain before it is switched on.");
        }

        IsEnabled = true;
    }

    public void SwitchOff()
    {
        RequireCompany();
        IsEnabled = false;
    }

    /// <summary>True when this company login is on and lists the address's domain.</summary>
    public bool Covers(string? email)
        => IsCompany && IsEnabled && DomainOf(email) is { } domain && Domains.Any(d => d.Domain == domain);

    /// <summary>The domain of an e-mail address, lower case, or null when the address has none.</summary>
    public static string? DomainOf(string? email)
    {
        var at = email?.LastIndexOf('@') ?? -1;
        if (email is null || at <= 0 || at == email.Length - 1)
        {
            return null;
        }

        return email[(at + 1)..].Trim().ToLowerInvariant();
    }

    /// <summary>True for a domain name like ritz.com: labels of letters, digits and hyphens, a top-level part of letters.</summary>
    public static bool IsDomainName(string? domain)
        => domain is { Length: > 0 and <= FederatedAuthorityDomain.DomainMaxLength } && DomainPattern().IsMatch(domain.Trim().ToLowerInvariant());

    public static bool IsPublicMailDomain(string domain) => PublicMailDomains.Contains(domain.Trim().ToLowerInvariant());

    /// <summary>The stored form of a domain: trimmed, lower case, checked.</summary>
    public static string DomainName(string domain)
    {
        if (!IsDomainName(domain))
        {
            throw new ArgumentException("A domain name like ritz.com is required.", nameof(domain));
        }

        return domain.Trim().ToLowerInvariant();
    }

    [GeneratedRegex(@"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainPattern();

    private void RequireCompany()
    {
        if (!IsCompany)
        {
            throw new InvalidOperationException("Harmony's own sign-in has no domains and is always on.");
        }
    }

    private static void RequireTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant is required.", nameof(tenantId));
        }
    }

    private static string Required(string? value, int maxLength, string argument)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is 0 || text.Length > maxLength)
        {
            throw new ArgumentException($"1 to {maxLength} characters are required.", argument);
        }

        return text;
    }
}
