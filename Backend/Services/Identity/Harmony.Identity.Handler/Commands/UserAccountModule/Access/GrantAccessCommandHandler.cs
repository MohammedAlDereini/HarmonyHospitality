namespace Harmony.Identity.Handler.Commands.UserAccountModule.Access;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

public class GrantAccessCommandHandler : AccessCommandHandlerBase, IRequestHandler<GrantAccessCommand, CallResponse<Guid>>
{
    public GrantAccessCommandHandler(IUserAccountRepository userAccounts, IRoleRepository roles, IPropertyRepository properties, IPropertyGroupRepository groups, ICallContext callContext, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, TimeProvider clock, ICacheService cache)
        : base(userAccounts, roles, properties, groups, callContext, auditEvents, securityEvents, clock, cache)
    {
    }

    public async Task<CallResponse<Guid>> Handle(GrantAccessCommand command, CancellationToken cancellationToken)
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

        var callerId = RankRules.CallerId(this.CallContext);
        var role = await this.RoleOfTenantAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Role.NotFound);
        }

        if (role.Rank > await this.UserAccounts.RankOfAsync(callerId, cancellationToken))
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
        var grant = user.Grant(role.Id, scope, command.FromUtc ?? now, command.ToUtc, callerId, command.Reason);
        await this.AuditEvents.AddAsync(this.SecurityEvents.Create(user, SecurityAuditActions.AccessGranted, $"{role.Code}@{scopeCode} until {grant.ToUtc:O}"), cancellationToken);
        await this.UserAccounts.SaveChangesAsync(cancellationToken);
        await this.ForgetAsync(user, cancellationToken);
        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(grant.Id);
    }
}
