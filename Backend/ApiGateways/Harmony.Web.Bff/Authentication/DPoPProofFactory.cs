namespace Harmony.Web.Bff.Authentication;

using System.Security.Cryptography;
using Harmony.Web.Bff.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Signs the DPoP proofs (RFC 9449) that bind this host's tokens to a key only it holds. Identity binds every token it
/// issues to the key's thumbprint; every call to the API then carries a fresh proof for that very method and address,
/// with the token's hash inside. A token copied from anywhere else is refused without this key. The key loads once at
/// startup: an unreadable key refuses the start, never a sign-in at 03:00.
/// </summary>
public sealed class DPoPProofFactory
{
    /// <summary>The proof's header type (RFC 9449 §4.2).</summary>
    public const string ProofType = "dpop+jwt";

    /// <summary>The request header that carries the proof, and the Authorization scheme of a bound token.</summary>
    public const string HeaderName = "DPoP";

    private readonly SigningCredentials signingCredentials;
    private readonly Dictionary<string, object> publicJwk;
    private readonly JsonWebTokenHandler handler = new();

    public DPoPProofFactory(IOptions<BffSettings> options)
    {
        var key = ECDsa.Create();
        key.ImportFromPem(File.ReadAllText(options.Value.DPoPPrivateKeyPath));
        var securityKey = new ECDsaSecurityKey(key);
        this.signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        // The public half travels in every proof's header; the thumbprint (RFC 7638) is what Identity writes into cnf.jkt.
        var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(securityKey);
        this.publicJwk = new Dictionary<string, object>
        {
            ["kty"] = jwk.Kty,
            ["crv"] = jwk.Crv,
            ["x"] = jwk.X,
            ["y"] = jwk.Y,
        };
        this.Thumbprint = Base64UrlEncoder.Encode(jwk.ComputeJwkThumbprint());
    }

    /// <summary>The key's RFC 7638 thumbprint: sent as dpop_jkt when the sign-in is pushed, so the code is bound too.</summary>
    public string Thumbprint { get; }

    /// <summary>
    /// A fresh one-use proof for one request: its method, its address without query or fragment, the moment, and the
    /// access token's hash when one is presented (never on a token request, which has no token yet). A nonce goes in only
    /// when the server asked for one.
    /// </summary>
    public string Create(HttpMethod method, Uri url, string? accessToken = null, string? nonce = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["jti"] = Guid.CreateVersion7().ToString("N"),
            ["htm"] = method.Method.ToUpperInvariant(),
            ["htu"] = url.GetLeftPart(UriPartial.Path),
        };
        if (accessToken is not null)
        {
            claims["ath"] = Base64UrlEncoder.Encode(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(accessToken)));
        }

        if (nonce is not null)
        {
            claims["nonce"] = nonce;
        }

        return this.handler.CreateToken(new SecurityTokenDescriptor
        {
            IssuedAt = DateTime.UtcNow,
            Claims = claims,
            SigningCredentials = this.signingCredentials,
            TokenType = ProofType,
            AdditionalHeaderClaims = new Dictionary<string, object> { ["jwk"] = this.publicJwk },
        });
    }
}
