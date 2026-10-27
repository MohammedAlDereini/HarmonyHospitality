namespace Harmony.Identity.Handler.Commands.UserAccountModule.Access;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.ApprovalModule;

/// <summary>The account, the rank rules, the role of this tenant, the scope of this tenant, one current assignment per role and scope; the trail; one save.</summary>
public class AssignRoleCommandHandler : AccessCommandHandlerBase, IRequestHandler<AssignRoleCommand, CallResponse<Guid>>
{
    public AssignRoleCommandHandler(IUserAccountRepository userAccounts, IRoleRepository roles, IPropertyRepository properties, IPropertyGroupRepository groups, ICallContext callContext, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, TimeProvider clock, ICacheService cache, IApprovalGate gate)
        : base(userAccounts, roles, properties, groups, callContext, auditEvents, securityEvents, clock, cache)
    {
        this.gate = gate;
    }

    private readonly IApprovalGate gate;

    public async Task<CallResponse<Guid>> Handle(AssignRoleCommand command, CancellationToken cancellationToken)
    {
        var user = await this.UserAccounts.GetWithAccessAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (await RankRules.RefusalAsync(this.UserAccounts, this.CallContext, user, cancellationToken) is { } refusal)
        {
            return Fail<Guid>(refusal);
        }

        var role = await this.RoleOfTenantAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Role.NotFound);
        }

        if (role.Rank > await this.UserAccounts.RankOfAsync(RankRules.CallerId(this.CallContext), cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Rank.AboveSelf);
        }

        var scope = AssignmentScope.Of(command.PropertyId, command.PropertyGroupId);
        var scopeCode = await this.ScopeCodeAsync(scope, cancellationToken);
        if (scopeCode is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.ScopeNotOfTenant);
        }

        var now = this.Clock.GetUtcNow();
        if (user.RoleAssignments.Any(a => a.RoleId == role.Id && a.Scope == scope && a.IsCurrentAt(now)))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.AssignmentExists);
        }

        // Stage D: an administrator-level role waits for a second administrator when the customer requires approvals.
        if (role.Rank >= RoleRank.Administrator
            && await this.gate.HoldAsync(ApprovalKinds.AdministratorAssignment, command, user.Id, [user.Id], cancellationToken) is { } hold)
        {
            return Fail<Guid>(hold);
        }

        var assignment = user.AssignRole(role.Id, scope, command.EndsAtUtc, now);
        await this.AuditEvents.AddAsync(this.SecurityEvents.Create(user, SecurityAuditActions.RoleAssigned, $"{role.Code}@{scopeCode}"), cancellationToken);
        await this.UserAccounts.SaveChangesAsync(cancellationToken);
        await this.ForgetAsync(user, cancellationToken);
        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(assignment.Id);
    }
}
