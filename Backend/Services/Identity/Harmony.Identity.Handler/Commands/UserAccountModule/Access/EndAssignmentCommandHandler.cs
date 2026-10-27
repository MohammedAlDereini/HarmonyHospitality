namespace Harmony.Identity.Handler.Commands.UserAccountModule.Access;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

public class EndAssignmentCommandHandler : AccessCommandHandlerBase, IRequestHandler<EndAssignmentCommand, CallResponse>
{
    public EndAssignmentCommandHandler(IUserAccountRepository userAccounts, IRoleRepository roles, IPropertyRepository properties, IPropertyGroupRepository groups, ICallContext callContext, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, TimeProvider clock, ICacheService cache)
        : base(userAccounts, roles, properties, groups, callContext, auditEvents, securityEvents, clock, cache)
    {
    }

    public async Task<CallResponse> Handle(EndAssignmentCommand command, CancellationToken cancellationToken)
    {
        var user = await this.UserAccounts.GetWithAccessAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (await RankRules.RefusalAsync(this.UserAccounts, this.CallContext, user, cancellationToken) is { } refusal)
        {
            return Fail(refusal);
        }

        var now = this.Clock.GetUtcNow();
        var assignment = user.RoleAssignments.FirstOrDefault(a => a.Id == command.AssignmentId);
        if (assignment is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.AssignmentNotFound);
        }

        if (!assignment.IsCurrentAt(now))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.AssignmentEnded);
        }

        user.EndAssignment(assignment.Id, now);
        var role = await this.RoleOfTenantAsync(assignment.RoleId, cancellationToken);
        var scopeCode = await this.ScopeCodeAsync(assignment.Scope, cancellationToken);
        await this.AuditEvents.AddAsync(this.SecurityEvents.Create(user, SecurityAuditActions.RoleAssignmentEnded, $"{role?.Code}@{scopeCode}"), cancellationToken);
        await this.UserAccounts.SaveChangesAsync(cancellationToken);
        await this.ForgetAsync(user, cancellationToken);
        return Ok();
    }
}
