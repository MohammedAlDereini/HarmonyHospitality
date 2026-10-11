using System.Buffers.Text;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Passkeys;

/// <summary>
/// A passkey row as the person sees it: never the public key. The credential id (base64url, as the browser knows it)
/// and the domain go to the browser's signal methods only, so a device forgets a removed key.
/// </summary>
internal static class PasskeyModel
{
    public static UserPasskeyModel From(UserPasskey row, string rpId) => new()
    {
        Id = row.Id,
        CredentialId = Base64Url.EncodeToString(row.CredentialId),
        RpId = rpId,
        Name = row.Name,
        CreatedUtc = row.CreatedUtc,
        LastUsedUtc = row.LastUsedUtc,
        IsBackedUp = row.IsBackedUp,
        IsBackupEligible = row.IsBackupEligible,
        Transports = row.Transports?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [],
    };
}
