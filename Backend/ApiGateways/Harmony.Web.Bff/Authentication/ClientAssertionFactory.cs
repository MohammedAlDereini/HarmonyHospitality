namespace Harmony.Web.Bff.Authentication;

using System.Security.Cryptography;
using Harmony.Web.Bff.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Signs the private_key_jwt (RFC 7523) that authenticates this host to Identity. No shared secret exists anywhere:
/// Identity holds only the public key, and nothing crossing the wire is reusable past its sixty seconds. The private
/// key loads once at startup — an unreadable key refuses the start, never a sign-in at 03:00.
/// </summary>
public sealed class ClientAssertionFactory
{
    public const string AssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly string clientId;
    private readonly SigningCredentials signingCredentials;
    private readonly JsonWebTokenHandler handler = new();

    public ClientAssertionFactory(IOptions<BffSettings> options)
    {
        this.clientId = options.Value.ClientId;

        // Kept for the host's whole life: the credentials below sign with it on every token call.
        var key = ECDsa.Create();
        key.ImportFromPem(File.ReadAllText(options.Value.ClientPrivateKeyPath));
        this.signingCredentials = new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256);
    }

    /// <summary>A fresh one-use assertion: this client, for this audience, dead in a minute.</summary>
    public string Create(string audience)
    {
        var now = DateTime.UtcNow;

        return this.handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = this.clientId,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(Lifetime),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = this.clientId,
                ["jti"] = Guid.CreateVersion7().ToString("N"),
            },
            SigningCredentials = this.signingCredentials,
            TokenType = "client-authentication+jwt",
        });
    }
}
