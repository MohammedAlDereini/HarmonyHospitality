namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

public class ReinstateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ReinstateUserAccountCommand, CallResponse>
{
    public ReinstateUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
    }

    public async Task<CallResponse> Handle(ReinstateUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
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
