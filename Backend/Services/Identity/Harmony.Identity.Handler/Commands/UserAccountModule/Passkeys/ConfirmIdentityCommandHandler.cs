namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using System.Text.Json;
using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Passkeys;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The proof itself. Password: the same rules as a sign-in (a locked-out account is refused, a wrong password counts
/// towards the lockout and goes in the trail). Passkey: the held challenge is spent, Identity's handler checks the
/// answer, and the key must be the caller's own. Either way the account is marked "confirmed" for 5 minutes and the
/// trail says how. Nothing else changes.
/// </summary>
public class ConfirmIdentityCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ConfirmIdentityCommand, CallResponse>
{
    private readonly IPasskeyHandler<User> passkeyHandler;
    private readonly IdentityConfirmation confirmation;
    private readonly IUserPasskeyRepository passkeys;
    private readonly IHttpContextAccessor http;
    private readonly ICallContext callContext;
    private readonly ILogger<ConfirmIdentityCommandHandler> logger;

    public ConfirmIdentityCommandHandler(
        UserManager<User> userManager,
        ICacheService cacheService,
        ISessionService sessionService,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        IPasskeyHandler<User> passkeyHandler,
        IdentityConfirmation confirmation,
        IUserPasskeyRepository passkeys,
        IHttpContextAccessor http,
        ICallContext callContext,
        ILogger<ConfirmIdentityCommandHandler> logger)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.passkeyHandler = passkeyHandler;
        this.confirmation = confirmation;
        this.passkeys = passkeys;
        this.http = http;
        this.callContext = callContext;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(ConfirmIdentityCommand command, CancellationToken cancellationToken)
    {
        var user = Guid.TryParse(this.callContext.UserId(), out var caller) ? await this.LoadAsync(caller) : null;
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        return string.IsNullOrEmpty(command.CredentialJson)
            ? await this.ByPasswordAsync(user, command.Password!, cancellationToken)
            : await this.ByPasskeyAsync(user, command.CredentialJson, cancellationToken);
    }

    private async Task<CallResponse> ByPasswordAsync(User user, string password, CancellationToken cancellationToken)
    {
        if (await this.UserManager.IsLockedOutAsync(user))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.LockedOut);
        }

        if (!await this.UserManager.CheckPasswordAsync(user, password))
        {
            // The same count as a wrong sign-in: five in a row lock the account for five minutes.
            await this.TrailAsync(user, SecurityAuditActions.SignInWrongPassword, cancellationToken);
            EnsureSucceeded(await this.UserManager.AccessFailedAsync(user));
            this.logger.LogInformation("Identity not confirmed for account {UserId}: wrong password.", user.Id);
            return Fail(BusinessErrorCodes.Identity.UserAccount.CurrentPasswordWrong);
        }

        await this.UserManager.ResetAccessFailedCountAsync(user);
        await this.ConfirmedAsync(user, "password", cancellationToken);
        return Ok();
    }

    private async Task<CallResponse> ByPasskeyAsync(User user, string credentialJson, CancellationToken cancellationToken)
    {
        var state = await this.confirmation.TakeChallengeAsync(user.Id, cancellationToken);
        if (state is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.PasskeyRegistrationExpired);
        }

        var request = this.http.HttpContext ?? throw new InvalidOperationException("A passkey proof needs a request.");
        PasskeyAssertionResult<User> assertion;
        try
        {
            assertion = await this.passkeyHandler.PerformAssertionAsync(new PasskeyAssertionContext
            {
                HttpContext = request,
                CredentialJson = credentialJson,
                AssertionState = state,
            });
        }
        catch (Exception exception) when (exception is PasskeyException or JsonException or ArgumentException or FormatException)
        {
            this.logger.LogInformation("Identity not confirmed for account {UserId}: the passkey answer could not be read ({Reason}).", user.Id, exception.Message);
            return Fail(BusinessErrorCodes.Identity.UserAccount.PasskeyRefused);
        }

        if (!assertion.Succeeded || assertion.User.Id != user.Id)
        {
            this.logger.LogInformation("Identity not confirmed for account {UserId}: {Reason}", user.Id, assertion.Failure?.Message ?? "the key is not the caller's");
            await this.TrailAsync(user, SecurityAuditActions.SignInPasskeyRefused, cancellationToken);
            await this.AuditEvents.SaveChangesAsync(cancellationToken);
            return Fail(BusinessErrorCodes.Identity.UserAccount.PasskeyRefused);
        }

        // A use of the key, like a sign-in: the counter and the backup state it reported now.
        var row = await this.passkeys.FindByCredentialAsync(user.Id, assertion.Passkey.CredentialId, cancellationToken);
        row?.Used(assertion.Passkey.SignCount, assertion.Passkey.IsBackedUp, DateTimeOffset.UtcNow);

        await this.ConfirmedAsync(user, "passkey", cancellationToken);
        return Ok();
    }

    private async Task ConfirmedAsync(User user, string how, CancellationToken cancellationToken)
    {
        await this.confirmation.MarkAsync(user.TenantId, user.Id, how, cancellationToken);
        await this.AuditEvents.AddAsync(this.SecurityEvents.Create(user, SecurityAuditActions.IdentityConfirmed, how), cancellationToken);
        await this.AuditEvents.SaveChangesAsync(cancellationToken);
        this.logger.LogInformation("Identity confirmed for account {UserId} by {How}.", user.Id, how);
    }
}
