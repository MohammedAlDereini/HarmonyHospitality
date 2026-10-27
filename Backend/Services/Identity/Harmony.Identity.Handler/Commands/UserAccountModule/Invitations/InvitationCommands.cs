namespace Harmony.Identity.Handler.Commands.UserAccountModule.Invitations;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.EnrolmentModule;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

/// <summary>The administrator sends a fresh invitation: the waiting one is cancelled and its link dies.</summary>
public class ResendInvitationCommand : BaseCommandRequest<CallResponse>
{
    /// <summary>The account.</summary>
    public Guid Id { get; set; }

    public class ResendInvitationCommandValidation : AbstractValidator<ResendInvitationCommand>
    {
        public ResendInvitationCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
        }
    }
}

/// <summary>The administrator withdraws the waiting invitation: its link dies; the account stays without a password.</summary>
public class CancelInvitationCommand : BaseCommandRequest<CallResponse>
{
    /// <summary>The account.</summary>
    public Guid Id { get; set; }

    public class CancelInvitationCommandValidation : AbstractValidator<CancelInvitationCommand>
    {
        public CancelInvitationCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
        }
    }
}

/// <summary>
/// What both doors share: the account inside the caller's customer (UserManager walls it), the rank rule, and an account
/// that has not chosen a password yet (00129 once it has). A new security stamp kills every link sent before.
/// </summary>
public abstract class InvitationCommandHandlerBase : UserAccountCommandHandlerBase
{
    protected InvitationCommandHandlerBase(
        UserManager<User> userManager,
        ICacheService cacheService,
        ISessionService sessionService,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        IUserAccountRepository userAccounts,
        ILocalStaffEnrolmentRepository enrolments,
        ICallContext callContext,
        TimeProvider clock)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.UserAccounts = userAccounts;
        this.Enrolments = enrolments;
        this.CallContext = callContext;
        this.Clock = clock;
    }

    protected IUserAccountRepository UserAccounts { get; }

    protected ILocalStaffEnrolmentRepository Enrolments { get; }

    protected ICallContext CallContext { get; }

    protected TimeProvider Clock { get; }

    protected async Task<(User? User, string? Refusal)> InviteeAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(id);
        if (user is null || user.State == AdministrativeState.Terminated)
        {
            return (null, BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (await RankRules.OutrankRefusalAsync(this.UserAccounts, RankRules.CallerId(this.CallContext), user, cancellationToken) is { } refusal)
        {
            return (null, refusal);
        }

        return user.PasswordHash is not null ? (null, BusinessErrorCodes.Identity.Enrolment.AlreadyAccepted) : (user, null);
    }
}

public class ResendInvitationCommandHandler : InvitationCommandHandlerBase, IRequestHandler<ResendInvitationCommand, CallResponse>
{
    private readonly IFederatedAuthorityRepository authorities;
    private readonly IAccountMailer mailer;

    public ResendInvitationCommandHandler(
        UserManager<User> userManager,
        ICacheService cacheService,
        ISessionService sessionService,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        IUserAccountRepository userAccounts,
        ILocalStaffEnrolmentRepository enrolments,
        IFederatedAuthorityRepository authorities,
        IAccountMailer mailer,
        ICallContext callContext,
        TimeProvider clock)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents, userAccounts, enrolments, callContext, clock)
    {
        this.authorities = authorities;
        this.mailer = mailer;
    }

    public async Task<CallResponse> Handle(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        var (user, refusal) = await this.InviteeAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return Fail(refusal!);
        }

        (await this.Enrolments.LatestOfAccountAsync(user.Id, cancellationToken))?.Cancel();
        EnsureSucceeded(await this.UserManager.UpdateSecurityStampAsync(user));

        var token = await this.UserManager.GeneratePasswordResetTokenAsync(user);
        await this.mailer.SendInvitationAsync(user, token, cancellationToken);

        var local = (await this.authorities.OfTenantAsync(user.TenantId, cancellationToken)).First(a => a.Scheme == AuthorityScheme.Local);
        await this.Enrolments.AddAsync(LocalStaffEnrolment.Invite(user.TenantId, RankRules.CallerId(this.CallContext), local.Id, user.Id, user.Email!, this.Clock.GetUtcNow()), cancellationToken);
        await this.TrailAsync(user, SecurityAuditActions.InvitationResent, cancellationToken);
        await this.Enrolments.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}

public class CancelInvitationCommandHandler : InvitationCommandHandlerBase, IRequestHandler<CancelInvitationCommand, CallResponse>
{
    public CancelInvitationCommandHandler(
        UserManager<User> userManager,
        ICacheService cacheService,
        ISessionService sessionService,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        IUserAccountRepository userAccounts,
        ILocalStaffEnrolmentRepository enrolments,
        ICallContext callContext,
        TimeProvider clock)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents, userAccounts, enrolments, callContext, clock)
    {
    }

    public async Task<CallResponse> Handle(CancelInvitationCommand command, CancellationToken cancellationToken)
    {
        var (user, refusal) = await this.InviteeAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return Fail(refusal!);
        }

        var invitation = await this.Enrolments.LatestOfAccountAsync(user.Id, cancellationToken);
        if (invitation is not { State: EnrolmentState.Pending })
        {
            return Fail(BusinessErrorCodes.Identity.Enrolment.NothingOpen);
        }

        invitation.Cancel();
        EnsureSucceeded(await this.UserManager.UpdateSecurityStampAsync(user));
        await this.TrailAsync(user, SecurityAuditActions.InvitationCancelled, cancellationToken);
        await this.Enrolments.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
