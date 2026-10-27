using System.Text.Json;
using Harmony.Core.Abstractions;
using Harmony.Core.Errors;
using Harmony.Identity.Domain.Entities.Aggregates.MakerCheckerModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

namespace Harmony.Identity.Infrastructure.Approvals;

/// <summary>
/// One per request scope. The maker must hold a live step-up (confirmed it is them in the last 5 minutes); the request
/// keeps the change as sent, and the security version of every guarded account, so the approval can tell if it moved.
/// </summary>
public sealed class ApprovalGate : IApprovalGate
{
    private readonly ITenantRepository tenants;
    private readonly IUserAccountRepository userAccounts;
    private readonly IStepUpAuthorizationRepository stepUps;
    private readonly IMakerCheckerRequestRepository requests;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;

    public ApprovalGate(ITenantRepository tenants, IUserAccountRepository userAccounts, IStepUpAuthorizationRepository stepUps, IMakerCheckerRequestRepository requests, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, TimeProvider clock)
    {
        this.tenants = tenants;
        this.userAccounts = userAccounts;
        this.stepUps = stepUps;
        this.requests = requests;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.callContext = callContext;
        this.clock = clock;
    }

    public Guid? Executing { get; private set; }

    public IDisposable Execute(Guid requestId)
    {
        this.Executing = requestId;
        return new Running(this);
    }

    public async Task<string?> HoldAsync(string kind, object change, Guid? target, IReadOnlyCollection<Guid> guarded, CancellationToken cancellationToken)
    {
        if (this.Executing is not null
            || !Guid.TryParse(this.callContext.TenantId(), out var tenantId)
            || await this.tenants.FindAsync(tenantId, cancellationToken) is not { ApprovalsRequired: true })
        {
            return null;
        }

        var makerId = Guid.Parse(this.callContext.UserId());
        var now = this.clock.GetUtcNow();
        var proof = await this.stepUps.LiveOfAccountAsync(makerId, now, cancellationToken);
        if (proof is null)
        {
            return BusinessErrorCodes.Identity.UserAccount.IdentityConfirmationRequired;
        }

        var versions = new List<(Guid, int)>();
        foreach (var id in guarded)
        {
            if (await this.userAccounts.FindByIdAcrossTenantsAsync(id, cancellationToken) is { } account)
            {
                versions.Add((account.Id, account.SecurityVersion));
            }
        }

        var request = MakerCheckerRequest.Raise(tenantId, makerId, proof.Id, kind, target, JsonSerializer.Serialize(change, change.GetType()), versions, now);
        await this.requests.AddAsync(request, cancellationToken);
        if (await this.userAccounts.FindByIdAcrossTenantsAsync(target ?? makerId, cancellationToken) is { } about)
        {
            await this.auditEvents.AddAsync(this.securityEvents.Create(about, SecurityAuditActions.ApprovalRequested, kind), cancellationToken);
        }

        await this.requests.SaveChangesAsync(cancellationToken);
        return BusinessErrorCodes.Identity.Approval.SentForApproval;
    }

    private sealed class Running(ApprovalGate gate) : IDisposable
    {
        public void Dispose() => gate.Executing = null;
    }
}
