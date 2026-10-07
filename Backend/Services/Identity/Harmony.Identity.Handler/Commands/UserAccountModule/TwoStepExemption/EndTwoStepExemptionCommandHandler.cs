namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoStepExemption;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// Off the two-step exception list. Every session of the account ends (SecurityVersion bump + refresh tokens removed), so a
/// password-only session cannot outlive the exemption; the next sign-in sets two-step up. Not on the list: nothing to do.
/// </summary>
public class EndTwoStepExemptionCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<EndTwoStepExemptionCommand, CallResponse>
{
    private readonly ICallContext callContext;
    private readonly IAccountMailer mailer;
    private readonly ILogger<EndTwoStepExemptionCommandHandler> logger;

    public EndTwoStepExemptionCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IAccountMailer mailer, ILogger<EndTwoStepExemptionCommandHandler> logger)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.callContext = callContext;
        this.mailer = mailer;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(EndTwoStepExemptionCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (!user.IsTwoStepExempt)
        {
            return Ok();
        }

        user.EndTwoStepExemption();
        await this.TrailAsync(user, SecurityAuditActions.TwoStepExemptionEnded, cancellationToken);
        await this.SaveAsync(user);
        await this.PublishSecurityVersionAsync(user, cancellationToken);

        this.logger.LogInformation("Account {UserId} taken off the two-step exception list by {AdminId}; SecurityVersion is now {Version}.", user.Id, this.callContext.UserId(), user.SecurityVersion);
        await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.TwoStepExemptionEnded, cancellationToken);

        return Ok();
    }
}
