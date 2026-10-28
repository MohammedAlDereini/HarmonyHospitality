namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

public class ReinstateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ReinstateUserAccountCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;

    public ReinstateUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserAccountRepository userAccounts, ICallContext callContext)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.userAccounts = userAccounts;
        this.callContext = callContext;
    }

    public async Task<CallResponse> Handle(ReinstateUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        // Stage E: the company directory owns this person's state and roles (IPSIE AL2, the Mews rule).
        if (user.IsDirectoryManaged)
        {
            return Fail(BusinessErrorCodes.Identity.Directory.DirectoryManaged);
        }


        // The rank rule: a target standing above the caller is not theirs to reinstate. (A suspended or terminated
        // account has no live session, so the caller's-own-account case cannot arise here.)
        if (await RankRules.OutrankRefusalAsync(this.userAccounts, RankRules.CallerId(this.callContext), user, cancellationToken) is { } refusal)
        {
            return Fail(refusal);
        }

        // Reinstate kills nothing, so the version stays and nothing is published: the user signs in again.
        var before = user.State;
        user.Reinstate();
        if (user.State != before)
        {
            await this.TrailAsync(user, SecurityAuditActions.AccountReinstated, cancellationToken);
        }

        await this.SaveAsync(user);

        return Ok();
    }
}
