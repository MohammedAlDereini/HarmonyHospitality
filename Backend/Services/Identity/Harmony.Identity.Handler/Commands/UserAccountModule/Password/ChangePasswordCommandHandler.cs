namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The caller changes their own password through the API. The account is the token's subject, never a body field, so
/// nobody can change another person's password here; the rules and what follows are <see cref="PasswordChangeHandlerBase"/>'s.
/// </summary>
public class ChangePasswordCommandHandler : PasswordChangeHandlerBase, IRequestHandler<ChangePasswordCommand, CallResponse>
{
    private readonly ICallContext callContext;

    public ChangePasswordCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IAccountMailer mailer, ILogger<ChangePasswordCommandHandler> logger)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents, mailer, logger)
    {
        this.callContext = callContext;
    }

    public async Task<CallResponse> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        // The framework copied the token's sub into the call context; anything else is not a person's token.
        if (!Guid.TryParse(this.callContext.UserId(), out var userId))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        var user = await this.LoadAsync(userId);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        return await this.ChangeAsync(user, command.CurrentPassword, command.NewPassword, cancellationToken);
    }
}
