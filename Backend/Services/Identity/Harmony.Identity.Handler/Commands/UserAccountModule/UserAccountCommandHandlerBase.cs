namespace Harmony.Identity.Handler.Commands.UserAccountModule;

using Harmony.Core.Exceptions;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Writes go through Identity's UserManager: it normalises names, checks uniqueness (inside the tenant,
/// because the store is tenant-filtered), keeps the security stamp, and saves through our DbContext.
/// After a save that bumped the user's SecurityVersion, the new number is published so every service refuses
/// the user's older tokens at the next request. Every security change also writes its row to the security trail.
/// </summary>
public abstract class UserAccountCommandHandlerBase
{

    protected UserAccountCommandHandlerBase(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents)
    {
        this.UserManager = userManager;
        this.CacheService = cacheService;
        this.SessionService = sessionService;
        this.AuditEvents = auditEvents;
        this.SecurityEvents = securityEvents;
    }

    protected UserManager<User> UserManager { get; }

    protected ICacheService CacheService { get; }

    protected ISessionService SessionService { get; }

    protected ISecurityAuditEventRepository AuditEvents { get; }

    protected ISecurityEventFactory SecurityEvents { get; }

    /// <summary>
    /// Stages the trail row for this change. It is not saved here: the handler's next save stores the change and its row
    /// together, or neither (the repository's add only stages, like every add in this service).
    /// </summary>
    protected async Task TrailAsync(User user, string action, CancellationToken cancellationToken)
    {
        await this.AuditEvents.AddAsync(this.SecurityEvents.Create(user, action), cancellationToken);
    }

    protected async Task<User?> LoadAsync(Guid id)
    {
        return await this.UserManager.FindByIdAsync(id.ToString());
    }

    protected async Task SaveAsync(User user)
    {
        EnsureSucceeded(await this.UserManager.UpdateAsync(user));
    }

    /// <summary>
    /// The cache follows the database at once. Called after the save, never before. A bumped version ends every
    /// session, so the refresh tokens the person holds go with it: nothing can be refreshed into a new token.
    /// </summary>
    protected async Task PublishSecurityVersionAsync(User user, CancellationToken cancellationToken)
    {
        await this.CacheService.AddAsync(
            SecurityVersionCacheKeys.UserVersionKey(user.TenantId, user.Id),
            user.SecurityVersion,
            SecurityVersionCacheKeys.UserVersionLifetime,
            cancellationToken);

        await this.SessionService.EndAllAsync(user.Id, cancellationToken);
    }

    /// <summary>An Identity refusal is a bug or a race, never user input, so it surfaces as an exception with the codes.</summary>
    protected static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var codes = string.Join(", ", result.Errors.Select(e => e.Code));
        throw new BusinessException($"The identity store refused the change: {codes}.", Error.New(BusinessErrorCodes.Identity.UserAccount.StoreRefused));
    }

    /// <summary>
    /// Why a new password was refused, as the one code the person can act on first: leaked, then the service name or
    /// their own name, then Identity's own rules (length, a symbol).
    /// </summary>
    protected static string PasswordRefusal(IEnumerable<string> identityCodes)
    {
        var codes = identityCodes.ToList();

        if (codes.Contains(PasswordRules.LeakedErrorCode))
        {
            return BusinessErrorCodes.Identity.UserAccount.PasswordLeaked;
        }

        return codes.Contains(PasswordRules.ContextWordErrorCode)
            ? BusinessErrorCodes.Identity.UserAccount.PasswordHasContextWord
            : BusinessErrorCodes.Identity.UserAccount.PasswordRejected;
    }

    protected static CallResponse Ok()
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);

    protected static CallResponse Fail(string errorCode)
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));

    protected static CallResponse<T> Fail<T>(string errorCode)
        => CallResponseBuilder.CreateResponse<T>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));
}
