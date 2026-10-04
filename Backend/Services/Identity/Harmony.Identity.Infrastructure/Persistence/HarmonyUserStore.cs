using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Persistence;

/// <summary>
/// Identity's user store with one change: the secrets Identity keeps in its token table, the authenticator key and the
/// recovery codes, are encrypted with Data Protection before they are stored and decrypted when read. Identity stores
/// them in clear by default; a copy of the database must not be enough to generate someone's codes.
/// </summary>
public sealed class HarmonyUserStore : UserOnlyStore<User, IdentityDbContext, Guid>
{
    // Identity's own name for the provider that holds the authenticator key and the recovery codes (UserManager keeps it internal).
    private const string InternalLoginProvider = "[AspNetUserStore]";
    private const string Purpose = "Harmony.Identity.UserTokens";

    private readonly IDataProtector protector;

    public HarmonyUserStore(IdentityDbContext context, IDataProtectionProvider dataProtection, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        this.protector = dataProtection.CreateProtector(Purpose);
    }

    public override Task SetTokenAsync(User user, string loginProvider, string name, string? value, CancellationToken cancellationToken)
    {
        var stored = IsSecret(loginProvider) && value is not null ? this.protector.Protect(value) : value;
        return base.SetTokenAsync(user, loginProvider, name, stored, cancellationToken);
    }

    public override async Task<string?> GetTokenAsync(User user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        var stored = await base.GetTokenAsync(user, loginProvider, name, cancellationToken);
        return IsSecret(loginProvider) && stored is not null ? this.protector.Unprotect(stored) : stored;
    }

    private static bool IsSecret(string loginProvider) => loginProvider == InternalLoginProvider;
}
