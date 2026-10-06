namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoStepExemption;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
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

    public ExemptFromTwoStepCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionRevoker sessionRevoker, IUserAccountRepository userAccounts, ICallContext callContext, IAccountMailer mailer, ILogger<ExemptFromTwoStepCommandHandler> logger)
        : base(userManager, cacheService, sessionRevoker)
    {
        this.userAccounts = userAccounts;
        this.callContext = callContext;
        this.mailer = mailer;
        this.logger = logger;
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

        if (await this.userAccounts.HoldsPrivilegedRoleAsync(user.Id, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.PrivilegedCannotBeExempt);
        }

        user.ExemptFromTwoStep(command.Reason);
        await this.SaveAsync(user);

        this.logger.LogInformation("Account {UserId} put on the two-step exception list by {AdminId}.", user.Id, this.callContext.UserId());
        await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.TwoStepExempted, cancellationToken);

        return Ok();
    }
}
