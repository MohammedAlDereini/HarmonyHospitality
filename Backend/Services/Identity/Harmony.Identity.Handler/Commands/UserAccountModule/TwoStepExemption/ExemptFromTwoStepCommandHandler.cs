namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoStepExemption;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.ApprovalModule;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The two-step exception list, like Mews: for a person who cannot use an app or a mailbox at work, the password alone signs
/// them in. Never the admin's own account, never a Manager or higher role (the design docs: privileged means two-step).
/// Nothing that is on goes off: with two-step already on, the person still proves it. The person is told by e-mail.
/// </summary>
public class ExemptFromTwoStepCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ExemptFromTwoStepCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;
    private readonly IAccountMailer mailer;
    private readonly ILogger<ExemptFromTwoStepCommandHandler> logger;
    private readonly IApprovalGate gate;

    public ExemptFromTwoStepCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserAccountRepository userAccounts, ICallContext callContext, IAccountMailer mailer, ILogger<ExemptFromTwoStepCommandHandler> logger, IApprovalGate gate)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.userAccounts = userAccounts;
        this.callContext = callContext;
        this.mailer = mailer;
        this.logger = logger;
        this.gate = gate;
    }

    public async Task<CallResponse> Handle(ExemptFromTwoStepCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (Guid.TryParse(this.callContext.UserId(), out var callerId) && callerId == user.Id)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.CannotExemptSelf);
        }

        // The rank rule: a target standing above the caller is not theirs to exempt.
        if (await RankRules.OutrankRefusalAsync(this.userAccounts, callerId, user, cancellationToken) is { } refusal)
        {
            return Fail(refusal);
        }

        if (await this.userAccounts.HoldsPrivilegedRoleAsync(user.Id, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.PrivilegedCannotBeExempt);
        }

        // The same thing again changes nothing. A new reason is recorded, but the person is told only when
        // their sign-in actually changes (going on the list), not when an admin rewords the reason.
        var wasExempt = user.IsTwoStepExempt;
        var reasonBefore = user.TwoStepExemptReason;

        // Stage D: going on the list waits for a second administrator when the customer requires approvals (rewording does not).
        if (!wasExempt && await this.gate.HoldAsync(ApprovalKinds.TwoStepExemption, command, user.Id, [user.Id], cancellationToken) is { } hold)
        {
            return Fail(hold);
        }

        user.ExemptFromTwoStep(command.Reason);
        if (user.TwoStepExemptReason == reasonBefore)
        {
            return Ok();
        }

        await this.TrailAsync(user, SecurityAuditActions.TwoStepExempted, cancellationToken);
        await this.SaveAsync(user);

        this.logger.LogInformation("Account {UserId} put on the two-step exception list by {AdminId}.", user.Id, this.callContext.UserId());
        if (!wasExempt)
        {
            await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.TwoStepExempted, cancellationToken);
        }

        return Ok();
    }
}
