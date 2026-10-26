namespace Harmony.Identity.Handler.Commands.UserAccountModule.Access;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

/// <summary>
/// What the four access doors share: the account with everything that gives it access, the rank rules (nobody shapes
/// their own access, nobody touches an account above them, nobody hands out a role above their own standing), the
/// role of this tenant, and the scope of this tenant (a walled hotel or group read; no hint which when not).
/// </summary>
public abstract class AccessCommandHandlerBase
{
    protected AccessCommandHandlerBase(
        IUserAccountRepository userAccounts,
        IRoleRepository roles,
        IPropertyRepository properties,
        IPropertyGroupRepository groups,
        ICallContext callContext,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        TimeProvider clock)
    {
        this.UserAccounts = userAccounts;
        this.Roles = roles;
        this.Properties = properties;
        this.Groups = groups;
        this.CallContext = callContext;
        this.AuditEvents = auditEvents;
        this.SecurityEvents = securityEvents;
        this.Clock = clock;
    }

    protected IUserAccountRepository UserAccounts { get; }

    protected IRoleRepository Roles { get; }

    protected IPropertyRepository Properties { get; }

    protected IPropertyGroupRepository Groups { get; }

    protected ICallContext CallContext { get; }

    protected ISecurityAuditEventRepository AuditEvents { get; }

    protected ISecurityEventFactory SecurityEvents { get; }

    protected TimeProvider Clock { get; }

    /// <summary>The role, the way UpdateUserRoles reads it: of this tenant, with its standing.</summary>
    protected sealed record RoleFacts(Guid Id, string Code, int Rank);

    protected async Task<RoleFacts?> RoleOfTenantAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await this.Roles.Query().Where(r => r.Id == roleId).Select(r => new { r.Id, r.Code, r.PrivilegeLevel, r.IsSuperRole }).FirstOrDefaultAsync(cancellationToken);
        return role is null ? null : new RoleFacts(role.Id, role.Code, RoleRank.Of(role.PrivilegeLevel, role.IsSuperRole));
    }

    /// <summary>The hotel's or the group's code, inside the wall; null when the scope is not this tenant's.</summary>
    protected async Task<string?> ScopeCodeAsync(AssignmentScope scope, CancellationToken cancellationToken)
        => scope.IsHotel
            ? await this.Properties.Query().Where(p => p.Id == scope.PropertyId).Select(p => p.Code).FirstOrDefaultAsync(cancellationToken)
            : await this.Groups.Query().Where(g => g.Id == scope.PropertyGroupId).Select(g => g.Code).FirstOrDefaultAsync(cancellationToken);

    protected static CallResponse Ok() => CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);

    protected static CallResponse Fail(string code) => CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));

    protected static CallResponse<T> Fail<T>(string code) => CallResponseBuilder.CreateResponse<T>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));
}
