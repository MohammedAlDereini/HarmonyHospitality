using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// PropX LoginCommandHandler, as Duende's password grant: the frontend posts username and password to /connect/token
/// and Duende issues the tokens. This class decides only "may this person sign in": the account exists, is Active,
/// is not locked out, and the password matches. Identity's own lockout counts the failures (5 in a row lock the
/// account for 5 minutes, PropX's rule). An unknown name and a wrong password get the same answer, so nobody can
/// learn which names exist. Sign-in runs before any tenant is known, so the lookup drops the tenant filter and
/// an optional tenant_id in the request picks the account when the same name exists in several tenants.
/// </summary>
public sealed class HarmonyPasswordValidator : IResourceOwnerPasswordValidator
{
    public const string TenantParameter = "tenant_id";
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountNotActive = "account_not_active";
    public const string LockedOut = "locked_out";
    public const string TenantRequired = "tenant_required";

    private readonly IdentityDbContext dbContext;
    private readonly UserManager<User> userManager;
    private readonly ILogger<HarmonyPasswordValidator> logger;

    public HarmonyPasswordValidator(IdentityDbContext dbContext, UserManager<User> userManager, ILogger<HarmonyPasswordValidator> logger)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.logger = logger;
    }

    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context, CancellationToken cancellationToken)
    {
        var name = this.userManager.NormalizeName(context.UserName?.Trim() ?? string.Empty);

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(context.Password))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, InvalidCredentials);
            return;
        }

        var candidates = await this.dbContext.UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(u => u.NormalizedUserName == name)
            .ToListAsync(cancellationToken);

        var requestedTenant = context.Request?.Raw?[TenantParameter];
        if (candidates.Count > 1 && !Guid.TryParse(requestedTenant, out _))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, TenantRequired);
            return;
        }

        var user = Guid.TryParse(requestedTenant, out var tenantId)
            ? candidates.SingleOrDefault(u => u.TenantId == tenantId)
            : candidates.SingleOrDefault();

        if (user is null)
        {
            this.logger.LogInformation("Sign-in refused: unknown account.");
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, InvalidCredentials);
            return;
        }

        if (!user.CanSignIn)
        {
            this.logger.LogInformation("Sign-in refused: account {UserId} is {State}.", user.Id, user.State);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, AccountNotActive);
            return;
        }

        if (await this.userManager.IsLockedOutAsync(user))
        {
            this.logger.LogWarning("Sign-in refused: account {UserId} is locked out.", user.Id);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, LockedOut);
            return;
        }

        if (!await this.userManager.CheckPasswordAsync(user, context.Password))
        {
            // Identity counts the failure; the fifth in a row locks the account for the configured window.
            await this.userManager.AccessFailedAsync(user);
            var locked = await this.userManager.IsLockedOutAsync(user);
            this.logger.LogInformation("Sign-in refused: wrong password for account {UserId}. LockedOut={LockedOut}", user.Id, locked);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, locked ? LockedOut : InvalidCredentials);
            return;
        }

        await this.userManager.ResetAccessFailedCountAsync(user);
        this.logger.LogInformation("Sign-in accepted for account {UserId} in tenant {Tenant}.", user.Id, user.TenantId);

        // The claims come from HarmonyProfileService; here only who signed in and how.
        context.Result = new GrantValidationResult(user.Id.ToString("D"), "pwd");
    }
}
