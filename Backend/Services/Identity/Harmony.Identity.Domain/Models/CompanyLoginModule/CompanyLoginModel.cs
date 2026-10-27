namespace Harmony.Identity.Domain.Models.CompanyLoginModule;

/// <summary>One company login of a customer (FederatedAuthorities, Microsoft or Google), with the domains it covers.</summary>
public class CompanyLoginModel
{
    public Guid Id { get; set; }

    /// <summary>Microsoft or Google.</summary>
    public string Provider { get; set; } = null!;

    /// <summary>Who signs the company's tokens; for Microsoft the company's tenant id is inside.</summary>
    public string Issuer { get; set; } = null!;

    public bool IsEnabled { get; set; }

    /// <summary>The e-mail domains it covers, in order.</summary>
    public List<string> Domains { get; set; } = [];

    /// <summary>The link the company's IT opens once (Microsoft's admin consent); null for Google.</summary>
    public string? ConsentUrl { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
