namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

public class TerminateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<TerminateUserAccountCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;

    public TerminateUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserAccountRepository userAccounts, ICallContext callContext)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.userAccounts = userAccounts;
        this.callContext = callContext;
    }

    public async Task<CallResponse> Handle(TerminateUserAccountCommand command, CancellationToken cancellationToken)
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


        // The rank rules: an admin door never serves the caller's own account, and never a target standing above the caller.
        if (await RankRules.RefusalAsync(this.userAccounts, this.callContext, user, cancellationToken) is { } refusal)
        {
            return Fail(refusal);
        }

        // Terminating twice is a no-op, and so is its trail.
        var before = user.State;
        user.Terminate(command.Reason);
        if (user.State != before)
        {
            await this.TrailAsync(user, SecurityAuditActions.AccountTerminated, cancellationToken);
        }

        await this.SaveAsync(user);

        // Terminate bumped the version: every token this user holds dies at its next request.
        await this.PublishSecurityVersionAsync(user, cancellationToken);

        return Ok();
    }
}
