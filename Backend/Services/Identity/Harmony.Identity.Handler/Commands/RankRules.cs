namespace Harmony.Identity.Handler.Commands;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;

/// <summary>
/// The rank rules shared by the admin doors. A person's standing is the highest privilege level among their roles
/// (<see cref="Domain.Entities.Aggregates.RoleModule.RoleRank"/>), and equals may act on equals: the doors refuse
/// only the caller's own account and a target standing above the caller. The data comes through the calling
/// handler's own repository, so the handler stays the only one talking to storage.
/// </summary>
internal static class RankRules
{
    /// <summary>The caller's account id. A token without one parses to Guid.Empty, which holds no roles: rank 0, so every guarded door refuses.</summary>
    public static Guid CallerId(ICallContext callContext)
    {
        _ = Guid.TryParse(callContext.UserId(), out var callerId);
        return callerId;
    }

    /// <summary>Both rules for a door aimed at an account: the error code to refuse with, or null to proceed.</summary>
    public static async Task<string?> RefusalAsync(IUserAccountRepository userAccounts, ICallContext callContext, User target, CancellationToken cancellationToken)
    {
        var callerId = CallerId(callContext);
        return callerId == target.Id
            ? BusinessErrorCodes.Identity.Rank.OwnAccount
            : await OutrankRefusalAsync(userAccounts, callerId, target, cancellationToken);
    }

    /// <summary>The standing rule alone, for the doors where the caller's own account is fine or unreachable.</summary>
    public static async Task<string?> OutrankRefusalAsync(IUserAccountRepository userAccounts, Guid callerId, User target, CancellationToken cancellationToken)
    {
        return await userAccounts.RankOfAsync(target.Id, cancellationToken) > await userAccounts.RankOfAsync(callerId, cancellationToken)
            ? BusinessErrorCodes.Identity.Rank.HigherRank
            : null;
    }
}
