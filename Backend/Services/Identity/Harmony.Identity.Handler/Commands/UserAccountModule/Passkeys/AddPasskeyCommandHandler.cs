namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using System.Text.Json;
using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Queries.UserAccountModule.Passkeys;
using Harmony.Identity.Infrastructure.Passkeys;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// The browser answered the creation options: the held challenge is spent, Identity's handler checks the answer (the
/// challenge, the origin, user verification, the key's algorithm, a credential id nobody else holds), and the passkey is
/// stored under the caller's name for it. The person is told by e-mail: a new way into the account was just added
/// (NIST SP 800-63B-4 asks for that notice). Current sessions stay: adding a passkey lowers nothing.
/// </summary>
public class AddPasskeyCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<AddPasskeyCommand, CallResponse<UserPasskeyModel>>
{
    /// <summary>The name when neither the caller nor the makers' table gives one; the web app opens the rename at once.</summary>
    public const string DefaultName = "Passkey";

    private readonly IPasskeyHandler<User> passkeyHandler;
    private readonly PasskeyRegistrationHold hold;
    private readonly IdentityConfirmation confirmation;
    private readonly IUserPasskeyRepository passkeys;
    private readonly IHttpContextAccessor http;
    private readonly ICallContext callContext;
    private readonly IAccountMailer mailer;
    private readonly ILogger<AddPasskeyCommandHandler> logger;
    private readonly IOptions<IdentityPasskeyOptions> passkeyOptions;

    public AddPasskeyCommandHandler(
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
        ICallContext callContext,
        IAccountMailer mailer,
        ILogger<AddPasskeyCommandHandler> logger,
        IOptions<IdentityPasskeyOptions> passkeyOptions)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.passkeyOptions = passkeyOptions;
        this.passkeyHandler = passkeyHandler;
        this.hold = hold;
        this.confirmation = confirmation;
        this.passkeys = passkeys;
        this.http = http;
        this.callContext = callContext;
        this.mailer = mailer;
        this.logger = logger;
    }

    public async Task<CallResponse<UserPasskeyModel>> Handle(AddPasskeyCommand command, CancellationToken cancellationToken)
    {
        var user = Guid.TryParse(this.callContext.UserId(), out var caller) ? await this.LoadAsync(caller) : null;
        if (user is null)
        {
            return Fail<UserPasskeyModel>(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (!await this.confirmation.IsConfirmedAsync(user.Id, cancellationToken))
        {
            return Fail<UserPasskeyModel>(BusinessErrorCodes.Identity.UserAccount.IdentityConfirmationRequired);
        }

        // Taken out before anything is checked: one challenge, one answer, whatever the answer says.
        var state = await this.hold.TakeAsync(user.Id, cancellationToken);
        if (state is null)
        {
            return Fail<UserPasskeyModel>(BusinessErrorCodes.Identity.UserAccount.PasskeyRegistrationExpired);
        }

        var request = this.http.HttpContext ?? throw new InvalidOperationException("A passkey registration needs a request.");
        PasskeyAttestationResult attestation;
        try
        {
            attestation = await this.passkeyHandler.PerformAttestationAsync(new PasskeyAttestationContext
            {
                HttpContext = request,
                CredentialJson = command.CredentialJson,
                AttestationState = state,
            });
        }
        catch (Exception exception) when (exception is PasskeyException or JsonException or ArgumentException or FormatException)
        {
            this.logger.LogInformation("Passkey refused for account {UserId}: the answer could not be read ({Reason}).", user.Id, exception.Message);
            return Fail<UserPasskeyModel>(BusinessErrorCodes.Identity.UserAccount.PasskeyRefused);
        }

        if (!attestation.Succeeded || !string.Equals(attestation.UserEntity.Id, user.Id.ToString("D"), StringComparison.Ordinal))
        {
            this.logger.LogInformation("Passkey refused for account {UserId}: {Reason}", user.Id, attestation.Failure?.Message ?? "the answer names another account");
            return Fail<UserPasskeyModel>(BusinessErrorCodes.Identity.UserAccount.PasskeyRefused);
        }

        if (await this.passkeys.CountAsync(user.Id, cancellationToken) >= UserPasskey.MaxPerAccount)
        {
            return Fail<UserPasskeyModel>(BusinessErrorCodes.Identity.UserAccount.PasskeyLimitReached);
        }

        // The trail row is staged first: Identity's save stores the passkey and the row together.
        // The name: the caller's, else the maker's ("Windows Hello", "Apple Passwords"), else "Passkey" to rename.
        var passkey = attestation.Passkey;
        passkey.Name = UserPasskey.NameOf(!string.IsNullOrWhiteSpace(command.Name) ? command.Name : PasskeyMakers.NameOf(passkey.AttestationObject) ?? DefaultName);
        await this.TrailAsync(user, SecurityAuditActions.PasskeyAdded, cancellationToken);
        EnsureSucceeded(await this.UserManager.AddOrUpdatePasskeyAsync(user, passkey));

        this.logger.LogInformation("Passkey added for account {UserId} (backed up: {BackedUp}).", user.Id, passkey.IsBackedUp);
        await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.PasskeyAdded, cancellationToken);

        var row = await this.passkeys.FindByCredentialAsync(user.Id, passkey.CredentialId, cancellationToken)
            ?? throw new InvalidOperationException("The passkey was saved but cannot be read back.");

        return CallResponseBuilder.CreateResponse<UserPasskeyModel>(eCallResponseStatus.Success).HasData(PasskeyModel.From(row, this.passkeyOptions.Value.ServerDomain!));
    }
}
