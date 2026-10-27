using Duende.IdentityServer;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Harmony.Identity.Infrastructure.CompanyLogin;

/// <summary>
/// Company login the way Duende documents external providers: ASP.NET Core's OpenID Connect handler, registered once per
/// provider, signing into Duende's external cookie; the callback page decides who the person is. A provider is registered
/// only when its settings are present. The handler checks the token (signature, audience, lifetime, nonce); the issuer
/// checks below are Microsoft's documented ones for a multi-tenant app; which customer the company is, and which account
/// the person is, the callback decides from Harmony's own tables.
/// </summary>
public static class CompanyLoginRegistration
{
    public static IServiceCollection AddCompanyLogin(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CompanyLoginSettings>()
            .Bind(configuration.GetSection(CompanyLoginSettings.Section))
            .Validate(settings => MicrosoftLoginSettings.IsValid(settings.Microsoft), $"{CompanyLoginSettings.Section}:Microsoft needs an https Instance ending in '/' and a ClientSecretPath naming a non-empty file when ClientId is set.")
            .Validate(settings => GoogleLoginSettings.IsValid(settings.Google), $"{CompanyLoginSettings.Section}:Google needs an https Authority and a ClientSecretPath naming a non-empty file when ClientId is set.")
            .ValidateOnStart();

        services.AddHttpClient(CompanyLoginDirectory.ClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Harmony-Identity");
        });
        services.TryAddScoped<ICompanyLoginDirectory, CompanyLoginDirectory>();

        var settings = configuration.GetSection(CompanyLoginSettings.Section).Get<CompanyLoginSettings>() ?? new CompanyLoginSettings();
        var authentication = services.AddAuthentication();

        if (settings.Microsoft.IsPresent)
        {
            var microsoft = settings.Microsoft;
            authentication.AddOpenIdConnect(CompanyLoginSchemes.Microsoft, "Microsoft", options =>
            {
                Common(options, microsoft.ClientId, microsoft.ClientSecretPath, "/signin-microsoft");
                options.Authority = microsoft.Authority;
                options.TokenValidationParameters.IssuerValidator = (issuer, token, _) => MicrosoftIssuer(issuer, token, microsoft);
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.IssuerSigningKeyValidatorUsingConfiguration = MicrosoftSigningKey;
                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    context.ProtocolMessage.LoginHint = Item(context.Properties, CompanyLoginSchemes.LoginHintItem);
                    context.ProtocolMessage.DomainHint = Item(context.Properties, CompanyLoginSchemes.DomainHintItem);
                    return Task.CompletedTask;
                };
            });
        }

        if (settings.Google.IsPresent)
        {
            var google = settings.Google;
            authentication.AddOpenIdConnect(CompanyLoginSchemes.Google, "Google", options =>
            {
                Common(options, google.ClientId, google.ClientSecretPath, "/signin-google");
                options.Authority = google.Authority;

                // Google writes its issuer with or without the scheme.
                options.TokenValidationParameters.ValidIssuers = [google.Authority, new Uri(google.Authority).Host];
                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    context.ProtocolMessage.LoginHint = Item(context.Properties, CompanyLoginSchemes.LoginHintItem);
                    if (Item(context.Properties, CompanyLoginSchemes.DomainHintItem) is { } domain)
                    {
                        context.ProtocolMessage.SetParameter("hd", domain);
                    }

                    return Task.CompletedTask;
                };
            });
        }

        return services;
    }

    private static void Common(OpenIdConnectOptions options, string clientId, string secretPath, string callbackPath)
    {
        options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
        options.ClientId = clientId;
        options.ClientSecret = CompanyLoginSettings.ReadSecret(secretPath)
            ?? throw new InvalidOperationException($"The company login secret file {secretPath} cannot be read.");
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.CallbackPath = callbackPath;

        // The claims as the provider wrote them (oid, tid, hd, email_verified), nothing renamed; no tokens kept.
        options.MapInboundClaims = false;

        // The handler drops iat by default; "confirm it is you" reads it (with auth_time) to know the sign-in is fresh.
        options.ClaimActions.Remove("iat");
        options.GetClaimsFromUserInfoEndpoint = false;
        options.SaveTokens = false;
        options.DisableTelemetry = true;

        // A refusal or a failure at the provider comes back to the sign-in page with a notice, never as an error page.
        options.Events.OnRemoteFailure = context =>
        {
            var returnUrl = Item(context.Properties, CompanyLoginSchemes.ReturnUrlItem);
            context.Response.Redirect(returnUrl is null
                ? "/account/error"
                : $"/account/login?notice=company-failed&returnUrl={Uri.EscapeDataString(returnUrl)}");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Microsoft's rule for a multi-tenant app: the token's tid is a GUID, and iss is exactly the instance's issuer for that
    /// tid. The tid is then part of the issuer the callback matches against a customer's company login.
    /// </summary>
    private static string MicrosoftIssuer(string issuer, SecurityToken token, MicrosoftLoginSettings microsoft)
    {
        var tenant = TenantClaim(token);
        if (tenant is null || !string.Equals(issuer, microsoft.IssuerOf(tenant), StringComparison.Ordinal))
        {
            throw new SecurityTokenInvalidIssuerException("The issuer is not the Microsoft issuer of the token's own tenant.") { InvalidIssuer = issuer };
        }

        return issuer;
    }

    /// <summary>
    /// Microsoft's signing-key issuer check: a key that names an issuer signs only for that issuer ({tenantid} stands for
    /// the token's own tenant). A key without one belongs to the whole instance.
    /// </summary>
    private static bool MicrosoftSigningKey(SecurityKey key, SecurityToken token, TokenValidationParameters parameters, BaseConfiguration configuration)
    {
        var jsonKey = key as JsonWebKey
            ?? (configuration as OpenIdConnectConfiguration)?.JsonWebKeySet?.Keys.FirstOrDefault(k => k.KeyId == key.KeyId);
        if (jsonKey is null || !jsonKey.AdditionalData.TryGetValue("issuer", out var named))
        {
            return true;
        }

        var keyIssuer = named switch
        {
            string text => text,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } element => element.GetString(),
            _ => null,
        };
        if (keyIssuer is null)
        {
            return false;
        }

        var tenant = TenantClaim(token);
        return tenant is not null
            && token is JsonWebToken jwt
            && string.Equals(keyIssuer.Replace("{tenantid}", tenant, StringComparison.Ordinal), jwt.Issuer, StringComparison.Ordinal);
    }

    private static string? TenantClaim(SecurityToken token)
        => token is JsonWebToken jwt
           && jwt.TryGetPayloadValue<string>("tid", out var tid)
           && Guid.TryParse(tid, out var parsed)
           && parsed.ToString("D") == tid
            ? tid
            : null;

    private static string? Item(AuthenticationProperties? properties, string key)
        => properties is not null && properties.Items.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;
}
