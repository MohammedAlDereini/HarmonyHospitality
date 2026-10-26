namespace Harmony.Identity.Handler.Commands.UserAccountModule.Access;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

public class RevokeGrantCommandHandler : AccessCommandHandlerBase, IRequestHandler<RevokeGrantCommand, CallResponse>
{
    public RevokeGrantCommandHandler(IUserAccountRepository userAccounts, IRoleRepository roles, IPropertyRepository properties, IPropertyGroupRepository groups, ICallContext callContext, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, TimeProvider clock)
        : base(userAccounts, roles, properties, groups, callContext, auditEvents, securityEvents, clock)
    {
    }

    public async Task<CallResponse> Handle(RevokeGrantCommand command, CancellationToken cancellationToken)
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
        var grant = user.AccessGrants.FirstOrDefault(g => g.Id == command.GrantId);
        if (grant is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.GrantNotFound);
        }

        if (grant.HasEndedAt(now))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.AssignmentEnded);
        }

        user.RevokeGrant(grant.Id, now);
        var role = await this.RoleOfTenantAsync(grant.RoleId, cancellationToken);
        var scopeCode = await this.ScopeCodeAsync(grant.Scope, cancellationToken);
        await this.AuditEvents.AddAsync(this.SecurityEvents.Create(user, SecurityAuditActions.AccessGrantRevoked, $"{role?.Code}@{scopeCode}"), cancellationToken);
        await this.UserAccounts.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
