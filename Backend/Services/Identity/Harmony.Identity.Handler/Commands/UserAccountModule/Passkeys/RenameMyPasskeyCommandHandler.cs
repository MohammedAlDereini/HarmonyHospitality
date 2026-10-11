namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// The caller renames one of their passkeys ("Old laptop"), like Mews and Microsoft let you. Only the label changes:
/// no key, no session, no e-mail; the trail notes it.
/// </summary>
public class RenameMyPasskeyCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<RenameMyPasskeyCommand, CallResponse>
{
    private readonly IUserPasskeyRepository passkeys;
    private readonly ICallContext callContext;

    public RenameMyPasskeyCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserPasskeyRepository passkeys, ICallContext callContext)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.passkeys = passkeys;
        this.callContext = callContext;
    }

    public async Task<CallResponse> Handle(RenameMyPasskeyCommand command, CancellationToken cancellationToken)
    {
        var user = Guid.TryParse(this.callContext.UserId(), out var caller) ? await this.LoadAsync(caller) : null;
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        var passkey = await this.passkeys.FindAsync(user.Id, command.PasskeyId, cancellationToken);
        if (passkey is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.PasskeyNotFound);
        }

        passkey.Rename(command.Name);
        await this.TrailAsync(user, SecurityAuditActions.PasskeyRenamed, cancellationToken);
        await this.passkeys.SaveChangesAsync(cancellationToken);

        return Ok();
    }
}
