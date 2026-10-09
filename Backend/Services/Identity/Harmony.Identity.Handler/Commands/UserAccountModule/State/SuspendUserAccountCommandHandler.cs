namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

public class SuspendUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<SuspendUserAccountCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;

    public SuspendUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserAccountRepository userAccounts, ICallContext callContext)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.userAccounts = userAccounts;
        this.callContext = callContext;
    }

    public async Task<CallResponse> Handle(SuspendUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }


        // The rank rules: an admin door never serves the caller's own account, and never a target standing above the caller.
        if (await RankRules.RefusalAsync(this.userAccounts, this.callContext, user, cancellationToken) is { } refusal)
        {
            return Fail(refusal);
        }

        // Suspending twice is a no-op, and so is its trail.
        var before = user.State;
        user.Suspend(command.Reason);
        if (user.State != before)
        {
            await this.TrailAsync(user, SecurityAuditActions.AccountSuspended, cancellationToken);
        }

        await this.SaveAsync(user);

        // Suspend bumped the version: every token this user holds dies at its next request.
        await this.PublishSecurityVersionAsync(user, cancellationToken);

        return Ok();
    }
}
