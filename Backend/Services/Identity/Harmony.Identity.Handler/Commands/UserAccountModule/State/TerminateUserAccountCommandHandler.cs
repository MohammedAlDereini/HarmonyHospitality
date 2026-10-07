namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

public class TerminateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<TerminateUserAccountCommand, CallResponse>
{
    public TerminateUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
    }

    public async Task<CallResponse> Handle(TerminateUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
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
