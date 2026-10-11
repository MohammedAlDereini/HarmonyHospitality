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
/// The caller wants to prove it is them with one of their passkeys: Identity's handler builds the request options with
/// the caller's own keys as the allowed ones (so the device offers only those), and the challenge is held 5 minutes for
/// the caller alone. A person with no passkey gets options with an empty allow list, which no device can answer: they
/// prove themselves with the password instead.
/// </summary>
public class ConfirmIdentityOptionsCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ConfirmIdentityOptionsCommand, CallResponse<PasskeyRequestOptionsModel>>
{
    private readonly IPasskeyHandler<User> passkeyHandler;
    private readonly IdentityConfirmation confirmation;
    private readonly IHttpContextAccessor http;
    private readonly ICallContext callContext;

    public ConfirmIdentityOptionsCommandHandler(
        UserManager<User> userManager,
        ICacheService cacheService,
        ISessionService sessionService,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        IPasskeyHandler<User> passkeyHandler,
        IdentityConfirmation confirmation,
        IHttpContextAccessor http,
        ICallContext callContext)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.passkeyHandler = passkeyHandler;
        this.confirmation = confirmation;
        this.http = http;
        this.callContext = callContext;
    }

    public async Task<CallResponse<PasskeyRequestOptionsModel>> Handle(ConfirmIdentityOptionsCommand command, CancellationToken cancellationToken)
    {
        var user = Guid.TryParse(this.callContext.UserId(), out var caller) ? await this.LoadAsync(caller) : null;
        if (user is null)
        {
            return Fail<PasskeyRequestOptionsModel>(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        var request = this.http.HttpContext ?? throw new InvalidOperationException("A passkey challenge needs a request.");
        var options = await this.passkeyHandler.MakeRequestOptionsAsync(user, request);
        await this.confirmation.HoldChallengeAsync(user.Id, options.AssertionState, cancellationToken);

        return CallResponseBuilder.CreateResponse<PasskeyRequestOptionsModel>(eCallResponseStatus.Success)
            .HasData(new PasskeyRequestOptionsModel { OptionsJson = options.RequestOptionsJson });
    }
}
