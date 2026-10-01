using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Harmony.Identity.Infrastructure.Signing;

/// <summary>
/// The RSA keys Identity signs tokens with, and the public half it publishes at /.well-known/jwks.json.
/// The private key never leaves this process: services fetch the public keys and can verify but never forge.
/// Every environment, Development included, runs from a real key file: no special cases, no key in git.
/// Singleton: keys are read once at start, and holding retired keys beside the current one is what
/// makes rotation a deployment rather than an outage.
/// </summary>
public sealed class RsaSigningKeyProvider : IDisposable
{
    public const int MinimumKeyBits = 2048;

    private readonly List<RSA> owned = [];
    private bool disposed;

    public RsaSigningKeyProvider(IOptions<TokenIssuerSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var value = settings.Value;

        this.Current = this.LoadCurrent(value);
        this.Retired = value.PreviousSigningKeyPems
            .Where(pem => !string.IsNullOrWhiteSpace(pem))
            .Select(this.Import)
            .ToList();
    }

    /// <summary>The key new tokens are signed with.</summary>
    public RsaSecurityKey Current { get; }

    /// <summary>Keys that may still verify a token in flight, but sign nothing.</summary>
    public IReadOnlyList<RsaSecurityKey> Retired { get; }

    /// <summary>RS256 with the current key: the one algorithm every service accepts.</summary>
    public SigningCredentials Credentials => new(this.Current, SecurityAlgorithms.RsaSha256);

    /// <summary>The public key set, current key first. Public parameters only, by construction.</summary>
    public IReadOnlyList<JsonWebKey> PublicKeys =>
        new[] { this.Current }.Concat(this.Retired).Select(ToPublicJsonWebKey).ToList();

    /// <summary>
    /// Reads a PEM-encoded RSA private key, or says why it cannot be used. Shared with the startup
    /// validation so a bad key fails the start, not the first token.
    /// </summary>
    public static bool TryDescribeProblem(string pem, out string problem)
    {
        problem = string.Empty;

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (ArgumentException)
        {
            problem = "not a readable PEM-encoded RSA private key";
            return true;
        }

        if (rsa.KeySize < MinimumKeyBits)
        {
            problem = $"{rsa.KeySize} bits; RS256 needs at least {MinimumKeyBits}";
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        foreach (var rsa in this.owned)
        {
            rsa.Dispose();
        }

        this.disposed = true;
    }

    private RsaSecurityKey LoadCurrent(TokenIssuerSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.SigningKeyPem))
        {
            return this.Import(settings.SigningKeyPem);
        }

        if (!string.IsNullOrWhiteSpace(settings.SigningKeyPath))
        {
            return this.Import(File.ReadAllText(settings.SigningKeyPath));
        }

        // Fatal, never generated: an instance that minted its own key would sign tokens no other
        // replica could verify, and the symptom would be 401s that move around the cluster.
        throw new InvalidOperationException(
            $"{TokenIssuerSettings.Section}:SigningKeyPem or :SigningKeyPath is required.");
    }

    private RsaSecurityKey Import(string pem)
    {
        if (TryDescribeProblem(pem, out var problem))
        {
            throw new InvalidOperationException($"{TokenIssuerSettings.Section}: the signing key is {problem}.");
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        this.owned.Add(rsa);
        return WithThumbprintKeyId(new RsaSecurityKey(rsa));
    }

    // The kid is the RFC 7638 thumbprint: derived from the key, so the same key has the same kid on
    // every instance and after every restart, and two keys can never share one.
    private static RsaSecurityKey WithThumbprintKeyId(RsaSecurityKey key)
    {
        key.KeyId = Base64UrlEncoder.Encode(ToPublicJsonWebKey(key).ComputeJwkThumbprint());
        return key;
    }

    private static JsonWebKey ToPublicJsonWebKey(RsaSecurityKey key)
    {
        // includePrivateParameters: false is the whole guard against publishing the private key.
        var publicOnly = new RsaSecurityKey(key.Rsa.ExportParameters(includePrivateParameters: false))
        {
            KeyId = key.KeyId,
        };

        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicOnly);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        return jwk;
    }
}