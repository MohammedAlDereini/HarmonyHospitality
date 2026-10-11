namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The admin door for a passkey that must go (a stolen laptop whose owner cannot reach the app): the same rank rules as
/// every admin door, then the passkey is removed, every session of the account ends (the stolen device's too) and the
/// person is told. The admin never sees the key itself.
/// </summary>
public class RemovePasskeyCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<RemovePasskeyCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IUserPasskeyRepository passkeys;
    private readonly ICallContext callContext;
    private readonly IAccountMailer mailer;
    private readonly ILogger<RemovePasskeyCommandHandler> logger;

    public RemovePasskeyCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserAccountRepository userAccounts, IUserPasskeyRepository passkeys, ICallContext callContext, IAccountMailer mailer, ILogger<RemovePasskeyCommandHandler> logger)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.userAccounts = userAccounts;
        this.passkeys = passkeys;
        this.callContext = callContext;
        this.mailer = mailer;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(RemovePasskeyCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        // The rank rules first: an unauthorized caller learns nothing about the account's passkeys.
        if (await RankRules.RefusalAsync(this.userAccounts, this.callContext, user, cancellationToken) is { } refusal)
        {
            return Fail(refusal);
        }

        var passkey = await this.passkeys.FindAsync(user.Id, command.PasskeyId, cancellationToken);
        if (passkey is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.PasskeyNotFound);
        }

        // The stolen laptop's session dies with its key: the version bumps in the same save, every session ends.
        user.EndAllSessions();
        await this.passkeys.DeleteAsync(passkey, cancellationToken);
        await this.TrailAsync(user, SecurityAuditActions.PasskeyRemovedByAdmin, cancellationToken);
        await this.SaveAsync(user);
        await this.PublishSecurityVersionAsync(user, cancellationToken);

        this.logger.LogInformation("Passkey removed by an admin from account {UserId}; SecurityVersion is now {Version}.", user.Id, user.SecurityVersion);
        await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.PasskeyRemoved, cancellationToken);

        return Ok();
    }
}
