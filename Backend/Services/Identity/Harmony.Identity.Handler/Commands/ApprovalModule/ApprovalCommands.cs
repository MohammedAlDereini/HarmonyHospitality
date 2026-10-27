namespace Harmony.Identity.Handler.Commands.ApprovalModule;

using System.Text.Json;
using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.MakerCheckerModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

/// <summary>A second administrator approves a waiting request: the change runs now, as the maker sent it.</summary>
public class ApproveRequestCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public class ApproveRequestCommandValidation : AbstractValidator<ApproveRequestCommand>
    {
        public ApproveRequestCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Approval.IdRequired);
        }
    }
}

/// <summary>A second administrator says no: nothing runs.</summary>
public class RejectRequestCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public class RejectRequestCommandValidation : AbstractValidator<RejectRequestCommand>
    {
        public RejectRequestCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Approval.IdRequired);
        }
    }
}

/// <summary>The customer's administrator switches approvals on (at once) or off (itself through an approval when on).</summary>
public class SetApprovalsRequiredCommand : BaseCommandRequest<CallResponse>
{
    public bool Required { get; set; }
}

public abstract class ApprovalCommandHandlerBase
{
    protected ApprovalCommandHandlerBase(IMakerCheckerRequestRepository requests, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, TimeProvider clock)
    {
        this.Requests = requests;
        this.UserAccounts = userAccounts;
        this.AuditEvents = auditEvents;
        this.SecurityEvents = securityEvents;
        this.CallContext = callContext;
        this.Clock = clock;
    }

    protected IMakerCheckerRequestRepository Requests { get; }

    protected IUserAccountRepository UserAccounts { get; }

    protected ISecurityAuditEventRepository AuditEvents { get; }

    protected ISecurityEventFactory SecurityEvents { get; }

    protected ICallContext CallContext { get; }

    protected TimeProvider Clock { get; }

    protected static CallResponse Fail(string code)
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));

    /// <summary>The waiting request, or the code that says why it cannot be decided by this caller.</summary>
    protected async Task<(MakerCheckerRequest? Request, string? Refusal)> DecidableAsync(Guid id, CancellationToken cancellationToken)
    {
        var request = await this.Requests.WithPartsAsync(id, cancellationToken);
        if (request is null)
        {
            return (null, BusinessErrorCodes.Identity.Approval.NotFound);
        }

        if (request.State != RequestState.Pending)
        {
            return (null, BusinessErrorCodes.Identity.Approval.NotPending);
        }

        return RankRules.CallerId(this.CallContext) == request.RequestedByUserAccountId
            ? (null, BusinessErrorCodes.Identity.Approval.CheckerIsMaker)
            : (request, null);
    }

    /// <summary>The trail line on the account the request is about (or the maker's, when it is about the customer).</summary>
    protected async Task TrailAsync(MakerCheckerRequest request, string action, CancellationToken cancellationToken)
    {
        if (await this.UserAccounts.FindByIdAcrossTenantsAsync(request.TargetUserAccountId ?? request.RequestedByUserAccountId, cancellationToken) is { } about)
        {
            await this.AuditEvents.AddAsync(this.SecurityEvents.Create(about, action, request.Kind), cancellationToken);
        }
    }
}

/// <summary>
/// Runs the stored change through its own handler, with every rule of that handler checked again against the checker, in
/// one transaction with the execution record. A guarded account that moved voids the request; a change its handler
/// refuses now rolls back and leaves the request waiting, with the handler's own code.
/// </summary>
public class ApproveRequestCommandHandler : ApprovalCommandHandlerBase, IRequestHandler<ApproveRequestCommand, CallResponse>
{
    private readonly IMediator mediator;
    private readonly IApprovalGate gate;

    public ApproveRequestCommandHandler(IMakerCheckerRequestRepository requests, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, TimeProvider clock, IMediator mediator, IApprovalGate gate)
        : base(requests, userAccounts, auditEvents, securityEvents, callContext, clock)
    {
        this.mediator = mediator;
        this.gate = gate;
    }

    public async Task<CallResponse> Handle(ApproveRequestCommand command, CancellationToken cancellationToken)
    {
        var (request, refusal) = await this.DecidableAsync(command.Id, cancellationToken);
        if (request is null)
        {
            return Fail(refusal!);
        }

        var now = this.Clock.GetUtcNow();
        var type = ApprovalKinds.CommandOf(request.Kind);
        var moved = type is null;
        foreach (var expected in request.ExpectedVersions)
        {
            var account = await this.UserAccounts.FindByIdAcrossTenantsAsync(expected.UserAccountId, cancellationToken);
            moved |= account is null || account.SecurityVersion != expected.ExpectedVersion;
        }

        if (moved)
        {
            request.Void(now);
            await this.TrailAsync(request, SecurityAuditActions.ApprovalVoided, cancellationToken);
            await this.Requests.SaveChangesAsync(cancellationToken);
            return Fail(BusinessErrorCodes.Identity.Approval.TargetChanged);
        }

        var change = JsonSerializer.Deserialize(request.Payload, type!)!;
        CallResponse? refused = null;
        try
        {
            await this.Requests.InTransaction().ExecuteAsync(async () =>
            {
                using (this.gate.Execute(request.Id))
                {
                    var answer = await this.mediator.Send(change, cancellationToken) as CallResponse;
                    if (answer is not { IsSuccess: true })
                    {
                        refused = answer;
                        throw new ApprovalRefusedException();
                    }
                }

                var touched = request.ExpectedVersions.Select(e => e.UserAccountId).Append(request.TargetUserAccountId ?? Guid.Empty).Where(id => id != Guid.Empty);
                request.Approve(RankRules.CallerId(this.CallContext), touched, now);
                await this.TrailAsync(request, SecurityAuditActions.ApprovalApproved, cancellationToken);
                await this.Requests.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (ApprovalRefusedException)
        {
            return refused ?? Fail(BusinessErrorCodes.Identity.Approval.NotPending);
        }

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }

    private sealed class ApprovalRefusedException : Exception
    {
    }
}

public class RejectRequestCommandHandler : ApprovalCommandHandlerBase, IRequestHandler<RejectRequestCommand, CallResponse>
{
    public RejectRequestCommandHandler(IMakerCheckerRequestRepository requests, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, TimeProvider clock)
        : base(requests, userAccounts, auditEvents, securityEvents, callContext, clock)
    {
    }

    public async Task<CallResponse> Handle(RejectRequestCommand command, CancellationToken cancellationToken)
    {
        var (request, refusal) = await this.DecidableAsync(command.Id, cancellationToken);
        if (request is null)
        {
            return Fail(refusal!);
        }

        request.Reject(RankRules.CallerId(this.CallContext), this.Clock.GetUtcNow());
        await this.TrailAsync(request, SecurityAuditActions.ApprovalRejected, cancellationToken);
        await this.Requests.SaveChangesAsync(cancellationToken);
        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}

public class SetApprovalsRequiredCommandHandler : IRequestHandler<SetApprovalsRequiredCommand, CallResponse>
{
    private readonly ITenantRepository tenants;
    private readonly IUserAccountRepository userAccounts;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly ICallContext callContext;
    private readonly IApprovalGate gate;

    public SetApprovalsRequiredCommandHandler(ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IApprovalGate gate)
    {
        this.tenants = tenants;
        this.userAccounts = userAccounts;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.callContext = callContext;
        this.gate = gate;
    }

    public async Task<CallResponse> Handle(SetApprovalsRequiredCommand command, CancellationToken cancellationToken)
    {
        var tenant = await this.tenants.FindAsync(Guid.Parse(this.callContext.TenantId()), cancellationToken);
        if (tenant is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.NotFound));
        }

        if (tenant.ApprovalsRequired == command.Required)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
        }

        // Off is itself a risky change: one administrator alone cannot switch the second pair of eyes away.
        if (!command.Required && await this.gate.HoldAsync(ApprovalKinds.ApprovalsOff, command, null, [], cancellationToken) is { } held)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(held));
        }

        tenant.RequireApprovals(command.Required);
        if (await this.userAccounts.FindByIdAcrossTenantsAsync(RankRules.CallerId(this.callContext), cancellationToken) is { } caller)
        {
            await this.auditEvents.AddAsync(this.securityEvents.Create(caller, command.Required ? SecurityAuditActions.ApprovalsSwitchedOn : SecurityAuditActions.ApprovalsSwitchedOff), cancellationToken);
        }

        await this.tenants.SaveChangesAsync(cancellationToken);
        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
