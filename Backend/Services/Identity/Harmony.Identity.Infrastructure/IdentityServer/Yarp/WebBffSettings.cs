using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Harmony.Identity.Infrastructure.IdentityServer.Yarp;

/// <summary>
/// The web app's backend-for-frontend (BFF, YARP): the server that signs people in with authorization code + PKCE as a
/// confidential client (RFC 10017 §6.1). It proves who it is with private_key_jwt (RFC 7523, recommended by RFC 9700 §2.5):
/// it signs each request with its own private key and Identity holds only the public key, so Identity keeps nothing that
/// could be stolen and replayed.
/// </summary>
public class WebBffSettings
{
    public const string Section = "IdentitySettings:WebBff";

    /// <summary>The BFF's client id at Duende.</summary>
    public const string ClientId = "harmony-web-bff";

    /// <summary>Where the BFF receives the one-time code after sign-in (ASP.NET Core's default OpenID Connect CallbackPath).</summary>
    public const string RedirectPath = "/signin-oidc";

    /// <summary>Where the browser lands after signing out (ASP.NET Core's default SignedOutCallbackPath).</summary>
    public const string PostLogoutRedirectPath = "/signout-callback-oidc";

    /// <summary>The BFF's origins, scheme + host + port and nothing after: "https://app.example.com".</summary>
    public string[] Origins { get; set; } = [];

    /// <summary>
    /// A PEM file with the PUBLIC half of the key the BFF signs with: EC P-256 (ES256, allowed by FAPI 2.0).
    /// Required; the service does not start without a valid one, or when the file holds a private key.
    /// </summary>
    public string ClientPublicKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Public keys still accepted during a rotation: the new one goes in <see cref="ClientPublicKeyPath"/>, the old one here
    /// until every BFF instance signs with the new one, then it is removed.
    /// </summary>
    public string[] PreviousClientPublicKeyPaths { get; set; } = [];

    /// <summary>
    /// The public key in the file as a JSON Web Key (RFC 7517) for Duende, or null when the file is missing, is not an
    /// EC P-256 public key, or holds a private key (Identity must never hold the private half).
    /// </summary>
    public static string? ReadPublicJwk(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var pem = File.ReadAllText(path);
        if (pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(pem);
            var parameters = key.ExportParameters(includePrivateParameters: false);
            if (parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
            {
                return null;
            }

            return JsonSerializer.Serialize(new
            {
                kty = "EC",
                crv = "P-256",
                x = Base64Url.EncodeToString(parameters.Q.X!),
                y = Base64Url.EncodeToString(parameters.Q.Y!),
                alg = "ES256",
                use = "sig",
            });
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return null;
        }
    }
}