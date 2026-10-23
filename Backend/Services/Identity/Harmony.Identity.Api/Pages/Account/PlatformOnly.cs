using Duende.IdentityServer.Models;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Infrastructure.IdentityServer;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// A platform-only client (the console) admits the platform tenant's people and nobody else. The sign-in page asks after
/// the credential passed, so a guesser learns nothing from the answer; the refusal is shown here, on Identity's own page,
/// the way Entra shows "assignment required" on its sign-in page, never sent back to the client as an error.
/// </summary>
internal static class PlatformOnly
{
    public const string Message = "This sign-in is for Harmony's own people. Hotel staff sign in at their hotel's address.";

    public static bool Admits(Client client, User user, Guid platformTenantId)
        => !IdentityServerResources.IsPlatformOnly(client) || user.TenantId == platformTenantId;
}
