namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Passkeys;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// The caller removes one of their passkeys (a lost phone, an old laptop) after proving it is them (00084 without).
/// The password and two-step stay as they are; every session ends, the lost device's too, and the person signs in
/// again once. The person is told by e-mail, as for any sign-in method that goes.
/// </summary>
public class RemoveMyPasskeyCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<RemoveMyPasskeyCommand, CallResponse>
{
    private readonly IUserPasskeyRepository passkeys;
    private readonly IdentityConfirmation confirmation;
    private readonly ICallContext callContext;
    private readonly IAccountMailer mailer;

    public RemoveMyPasskeyCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserPasskeyRepository passkeys, IdentityConfirmation confirmation, ICallContext callContext, IAccountMailer mailer)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.passkeys = passkeys;
        this.confirmation = confirmation;
        this.callContext = callContext;
        this.mailer = mailer;
    }

    public async Task<CallResponse> Handle(RemoveMyPasskeyCommand command, CancellationToken cancellationToken)
    {
        var user = Guid.TryParse(this.callContext.UserId(), out var caller) ? await this.LoadAsync(caller) : null;
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (!await this.confirmation.IsConfirmedAsync(user.Id, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.IdentityConfirmationRequired);
        }

        var passkey = await this.passkeys.FindAsync(user.Id, command.PasskeyId, cancellationToken);
        if (passkey is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.PasskeyNotFound);
        }

        // A credential changed, so every session ends (Microsoft's rule): the version bumps in the same save as the
        // removal, and the person signs in again once, with any other key or the password.
        user.EndAllSessions();
        await this.passkeys.DeleteAsync(passkey, cancellationToken);
        await this.TrailAsync(user, SecurityAuditActions.PasskeyRemoved, cancellationToken);
        await this.SaveAsync(user);
        await this.PublishSecurityVersionAsync(user, cancellationToken);
        await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.PasskeyRemoved, cancellationToken);

        return Ok();
    }
}
