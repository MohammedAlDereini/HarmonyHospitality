using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Persistence;

/// <summary>
/// Identity's user store with three changes. The secrets Identity keeps in its token table, the authenticator key and
/// the recovery codes, are encrypted with Data Protection before they are stored and decrypted when read (Identity
/// stores them in clear by default; a copy of the database must not be enough to generate someone's codes). Passkeys
/// live in our own UserPasskeys table (Identity's store expects its AspNetUserPasskeys shape, which this context does
/// not map). And a lookup by id with no tenant on the call (the sign-in pages) searches every tenant, as the pages'
/// own lookups do: Identity's passkey handler finds the account by the id the credential carries.
/// </summary>
public sealed class HarmonyUserStore : UserOnlyStore<User, IdentityDbContext, Guid>
{
    // Identity's own name for the provider that holds the authenticator key and the recovery codes (UserManager keeps it internal).
    private const string InternalLoginProvider = "[AspNetUserStore]";
    private const string Purpose = "Harmony.Identity.UserTokens";

    private readonly IDataProtector protector;
    private readonly ICallContext callContext;

    public HarmonyUserStore(IdentityDbContext context, IDataProtectionProvider dataProtection, ICallContext callContext, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        this.protector = dataProtection.CreateProtector(Purpose);
        this.callContext = callContext;
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

    /// <summary>
    /// With a tenant on the call (every API call), Identity's own lookup inside the tenant wall. Without one (the sign-in
    /// pages, where the tenant filter matches nothing), every tenant: the id is unique, and that is how the pages look
    /// accounts up already.
    /// </summary>
    public override Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (this.CallerHasTenant())
        {
            return base.FindByIdAsync(userId, cancellationToken);
        }

        if (!Guid.TryParse(userId, out var id))
        {
            return Task.FromResult<User?>(null);
        }

        return this.Context.UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    // ---- passkeys (IUserPasskeyStore<User>), on our UserPasskeys table

    public override async Task AddOrUpdatePasskeyAsync(User user, UserPasskeyInfo passkey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(passkey);

        var row = await this.Passkeys()
            .FirstOrDefaultAsync(p => p.UserAccountId == user.Id && p.CredentialId == passkey.CredentialId, cancellationToken);

        if (row is null)
        {
            this.Context.UserPasskeys.Add(UserPasskey.Register(
                user.TenantId,
                user.Id,
                passkey.Name!,
                passkey.CredentialId,
                passkey.PublicKey,
                passkey.SignCount,
                passkey.Transports,
                passkey.IsUserVerified,
                passkey.IsBackupEligible,
                passkey.IsBackedUp,
                passkey.AttestationObject,
                passkey.ClientDataJson,
                passkey.CreatedAt));
            return;
        }

        // Identity's "update" (a rename, Microsoft's shape): the name and the authenticator's state. The sign-in page
        // records a use itself, through the repository, so "last used" is never moved by an update.
        if (!string.IsNullOrWhiteSpace(passkey.Name))
        {
            row.Rename(passkey.Name);
        }

        row.Updated(passkey.SignCount, passkey.IsBackedUp);
    }

    public override async Task<IList<UserPasskeyInfo>> GetPasskeysAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var rows = await this.Passkeys()
            .Where(p => p.UserAccountId == user.Id)
            .OrderBy(p => p.CreatedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(ToInfo).ToList();
    }

    public override async Task<User?> FindByPasskeyIdAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentialId);

        // Before any tenant is known (sign-in) and across tenants on purpose (a duplicate credential id is refused wherever it is).
        var row = await this.Passkeys().FirstOrDefaultAsync(p => p.CredentialId == credentialId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        return await this.Context.UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(u => u.Id == row.UserAccountId, cancellationToken);
    }

    public override async Task<UserPasskeyInfo?> FindPasskeyAsync(User user, byte[] credentialId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(credentialId);

        var row = await this.Passkeys()
            .FirstOrDefaultAsync(p => p.UserAccountId == user.Id && p.CredentialId == credentialId, cancellationToken);
        return row is null ? null : ToInfo(row);
    }

    public override async Task RemovePasskeyAsync(User user, byte[] credentialId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(credentialId);

        var row = await this.Passkeys()
            .FirstOrDefaultAsync(p => p.UserAccountId == user.Id && p.CredentialId == credentialId, cancellationToken);
        if (row is not null)
        {
            this.Context.UserPasskeys.Remove(row);
        }
    }

    private static UserPasskeyInfo ToInfo(UserPasskey row)
        => new(
            row.CredentialId,
            row.PublicKey,
            row.CreatedUtc,
            (uint)Math.Clamp(row.SignCount, 0, uint.MaxValue),
            row.Transports?.Split(','),
            row.IsUserVerified,
            row.IsBackupEligible,
            row.IsBackedUp,
            row.AttestationObject,
            row.ClientDataJson)
        {
            Name = row.Name,
        };

    // Passkeys are read before any tenant is known (sign-in), so the tenant wall is lifted; the account id is the wall.
    private IQueryable<UserPasskey> Passkeys()
        => this.Context.UserPasskeys.IgnoreQueryFilters([QueryFilterNames.Tenant]);

    private bool CallerHasTenant()
        => Guid.TryParse(this.callContext.TenantId(), out var tenantId) && tenantId != Guid.Empty;

    private static bool IsSecret(string loginProvider) => loginProvider == InternalLoginProvider;
}
