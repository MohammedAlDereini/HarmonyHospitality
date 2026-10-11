namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Passkeys;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// The caller starts adding a passkey: Identity's handler builds the WebAuthn creation options (a fresh challenge, the
/// caller as the key's owner, the passkeys they already hold excluded) and the challenge is held 5 minutes for the
/// caller alone. Refused without a fresh proof of identity (00084) and when the account already holds its ten passkeys.
/// </summary>
public class StartPasskeyRegistrationCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<StartPasskeyRegistrationCommand, CallResponse<PasskeyCreationOptionsModel>>
{
    private readonly IPasskeyHandler<User> passkeyHandler;
    private readonly PasskeyRegistrationHold hold;
    private readonly IdentityConfirmation confirmation;
    private readonly IUserPasskeyRepository passkeys;
    private readonly IHttpContextAccessor http;
    private readonly ICallContext callContext;

    public StartPasskeyRegistrationCommandHandler(
        UserManager<User> userManager,
        ICacheService cacheService,
        ISessionService sessionService,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        IPasskeyHandler<User> passkeyHandler,
        PasskeyRegistrationHold hold,
        IdentityConfirmation confirmation,
        IUserPasskeyRepository passkeys,
        IHttpContextAccessor http,
        ICallContext callContext)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.passkeyHandler = passkeyHandler;
        this.hold = hold;
        this.confirmation = confirmation;
        this.passkeys = passkeys;
        this.http = http;
        this.callContext = callContext;
    }

    public async Task<CallResponse<PasskeyCreationOptionsModel>> Handle(StartPasskeyRegistrationCommand command, CancellationToken cancellationToken)
    {
        var user = Guid.TryParse(this.callContext.UserId(), out var caller) ? await this.LoadAsync(caller) : null;
        if (user is null)
        {
            return Fail<PasskeyCreationOptionsModel>(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        // A session may be hours old; a passkey is a permanent key. The person proves it is them first (5 minutes).
        if (!await this.confirmation.IsConfirmedAsync(user.Id, cancellationToken))
        {
            return Fail<PasskeyCreationOptionsModel>(BusinessErrorCodes.Identity.UserAccount.IdentityConfirmationRequired);
        }

        if (await this.passkeys.CountAsync(user.Id, cancellationToken) >= UserPasskey.MaxPerAccount)
        {
            return Fail<PasskeyCreationOptionsModel>(BusinessErrorCodes.Identity.UserAccount.PasskeyLimitReached);
        }

        var request = this.http.HttpContext ?? throw new InvalidOperationException("A passkey registration needs a request.");
        var owner = new PasskeyUserEntity { Id = user.Id.ToString("D"), Name = user.UserName!, DisplayName = user.DisplayName };
        var options = await this.passkeyHandler.MakeCreationOptionsAsync(owner, request);
        await this.hold.HoldAsync(user.Id, options.AttestationState, cancellationToken);

        return CallResponseBuilder.CreateResponse<PasskeyCreationOptionsModel>(eCallResponseStatus.Success)
            .HasData(new PasskeyCreationOptionsModel { OptionsJson = options.CreationOptionsJson });
    }
}
