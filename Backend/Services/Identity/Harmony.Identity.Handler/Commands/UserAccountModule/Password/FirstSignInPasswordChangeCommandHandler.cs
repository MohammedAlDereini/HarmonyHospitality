namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The forced change at first sign-in. The account must still carry MustChangePassword: once the flag is clear the walk
/// that led here is stale, and the door stays shut (the same answer as an unknown account, nothing to learn from it).
/// </summary>
public class FirstSignInPasswordChangeCommandHandler : PasswordChangeHandlerBase, IRequestHandler<FirstSignInPasswordChangeCommand, CallResponse>
{
    public FirstSignInPasswordChangeCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IAccountMailer mailer, ILogger<FirstSignInPasswordChangeCommandHandler> logger)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents, mailer, logger)
    {
    }

    public async Task<CallResponse> Handle(FirstSignInPasswordChangeCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.UserId);
        if (user is null || !user.MustChangePassword)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        return await this.ChangeAsync(user, command.CurrentPassword, command.NewPassword, cancellationToken);
    }
}
