using Harmony.Identity.Infrastructure.Mail;

namespace Harmony.Identity.Infrastructure.CompanyLogin;

/// <summary>
/// Harmony's own apps at Microsoft and Google (stage A, company login). One app per provider serves every customer; a
/// customer's company is a FederatedAuthorities row. No section, or no ClientId, means that provider is not offered.
/// </summary>
public sealed class CompanyLoginSettings
{
    public const string Section = "IdentitySettings:CompanyLogin";

    public MicrosoftLoginSettings Microsoft { get; set; } = new();

    public GoogleLoginSettings Google { get; set; } = new();

    /// <summary>
    /// Where the sign-in page's form may send the browser on to: the configured providers' sign-in hosts. A browser applies
    /// the page's form-action rule to the redirect after the post too, so without them the company sign-in is blocked.
    /// </summary>
    public IEnumerable<string> SignInOrigins()
    {
        if (this.Microsoft.IsPresent)
        {
            yield return new Uri(this.Microsoft.Instance).GetLeftPart(UriPartial.Authority);
        }

        if (this.Google.IsPresent)
        {
            yield return new Uri(this.Google.Authority).GetLeftPart(UriPartial.Authority);
        }
    }

    /// <summary>The client secret in a file (a mounted secret in production, secrets/ in development); null when unreadable.</summary>
    public static string? ReadSecret(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var secret = File.ReadAllText(path).Trim();
        return secret.Length == 0 ? null : secret;
    }
}

/// <summary>Harmony's multi-tenant app in Microsoft Entra ID: any company signs in through its "organizations" endpoint.</summary>
public sealed class MicrosoftLoginSettings
{
    /// <summary>The Microsoft sign-in host with its trailing slash. Changed only to point the tests at a local stand-in.</summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecretPath { get; set; } = string.Empty;

    public bool IsPresent => this.ClientId.Length > 0;

    /// <summary>Signs in a work account of any company; the company is checked by Harmony afterwards.</summary>
    public string Authority => $"{this.Instance}organizations/v2.0";

    /// <summary>The issuer Microsoft writes into a company's tokens: the company's tenant id inside.</summary>
    public string IssuerOf(string tenantId) => $"{this.Instance}{tenantId}/v2.0";

    /// <summary>Microsoft's public metadata for a company, found by any of its domains or its tenant id.</summary>
    public string MetadataOf(string domainOrTenant) => $"{this.Instance}{Uri.EscapeDataString(domainOrTenant)}/v2.0/.well-known/openid-configuration";

    /// <summary>The tenant id inside an issuer of this instance, or null when the issuer is not one.</summary>
    public string? TenantOf(string issuer)
    {
        const string Suffix = "/v2.0";
        if (!issuer.StartsWith(this.Instance, StringComparison.Ordinal) || !issuer.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return null;
        }

        var middle = issuer[this.Instance.Length..^Suffix.Length];
        return Guid.TryParse(middle, out var tenant) && tenant.ToString("D") == middle ? middle : null;
    }

    public static bool IsValid(MicrosoftLoginSettings settings)
        => !settings.IsPresent
            || (WebLinkSettings.IsValidOrigin(settings.Instance.TrimEnd('/')) && settings.Instance.EndsWith('/') && CompanyLoginSettings.ReadSecret(settings.ClientSecretPath) is not null);
}

/// <summary>Harmony's app at Google: Workspace companies sign in through it; the company is its hosted domain (hd).</summary>
public sealed class GoogleLoginSettings
{
    /// <summary>Google's issuer and sign-in host. Changed only to point the tests at a local stand-in.</summary>
    public string Authority { get; set; } = "https://accounts.google.com";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecretPath { get; set; } = string.Empty;

    public bool IsPresent => this.ClientId.Length > 0;

    public static bool IsValid(GoogleLoginSettings settings)
        => !settings.IsPresent
            || (WebLinkSettings.IsValidOrigin(settings.Authority) && CompanyLoginSettings.ReadSecret(settings.ClientSecretPath) is not null);
}
