using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;

namespace Harmony.Identity.Infrastructure.CompanyLogin;

/// <summary>The ASP.NET Core authentication schemes of the company logins, and the hints a sign-in carries to the provider.</summary>
public static class CompanyLoginSchemes
{
    public const string Microsoft = "microsoft";
    public const string Google = "google";

    /// <summary>The e-mail the person typed, passed as login_hint so they do not type it again.</summary>
    public const string LoginHintItem = "login_hint";

    /// <summary>The e-mail's domain: Microsoft's domain_hint, Google's hd.</summary>
    public const string DomainHintItem = "domain_hint";

    /// <summary>Where the sign-in goes back to (the authorization request), kept in the external cookie's properties.</summary>
    public const string ReturnUrlItem = "returnUrl";

    /// <summary>Which provider the person went to, kept in the external cookie's properties for the callback.</summary>
    public const string SchemeItem = "scheme";

    public static string Of(AuthorityScheme scheme) => scheme == AuthorityScheme.Google ? Google : Microsoft;

    public static AuthorityScheme? SchemeOf(string? name) => name switch
    {
        Microsoft => AuthorityScheme.Microsoft,
        Google => AuthorityScheme.Google,
        _ => null,
    };
}
