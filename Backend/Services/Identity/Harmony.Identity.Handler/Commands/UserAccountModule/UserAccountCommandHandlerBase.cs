namespace Harmony.Identity.Handler.Commands.UserAccountModule;

using Harmony.Core.Exceptions;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Writes go through Identity's UserManager: it normalises names, checks uniqueness (inside the tenant,
/// because the store is tenant-filtered), keeps the security stamp, and saves through our DbContext.
/// </summary>
public abstract class UserAccountCommandHandlerBase
{
    protected UserAccountCommandHandlerBase(UserManager<User> userManager)
    {
        this.UserManager = userManager;
    }

    protected UserManager<User> UserManager { get; }

    protected async Task<User?> LoadAsync(Guid id)
    {
        return await this.UserManager.FindByIdAsync(id.ToString());
    }

    protected async Task SaveAsync(User user)
    {
        EnsureSucceeded(await this.UserManager.UpdateAsync(user));
    }

    /// <summary>An Identity refusal is a bug or a race, never user input, so it surfaces as an exception with the codes.</summary>
    protected static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var codes = string.Join(", ", result.Errors.Select(e => e.Code));
        throw new BusinessException($"The identity store refused the change: {codes}.", Error.New(IdentityErrorCodes.IdentityStoreRefused));
    }

    protected static CallResponse Ok()
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);

    protected static CallResponse Fail(string errorCode)
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));

    protected static CallResponse<T> Fail<T>(string errorCode)
        => CallResponseBuilder.CreateResponse<T>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));
}
