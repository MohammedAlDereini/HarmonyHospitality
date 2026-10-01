using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Harmony.Identity.Infrastructure.Signing;
using Microsoft.IdentityModel.Tokens;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// Duende signs with OUR key ring (automatic key management is off): the current key signs, and every
/// retired key stays published so a rotation never invalidates a token in flight.
/// </summary>
public static class KeyRingStores
{
    public static ISigningCredentialStore Signing(RsaSigningKeyProvider keys)
        => new InMemorySigningCredentialsStore(keys.Credentials);

    public static IValidationKeysStore Validation(RsaSigningKeyProvider keys)
        => new InMemoryValidationKeysStore(
            new[] { keys.Current }.Concat(keys.Retired)
                .Select(key => new SecurityKeyInfo { Key = key, SigningAlgorithm = SecurityAlgorithms.RsaSha256 }));
}
