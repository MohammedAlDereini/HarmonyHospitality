namespace Harmony.Identity.Handler.Commands.TenantModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// What the tenant doors share. They are platform doors: only a person of the platform tenant (us) may call them, the
/// permission alone is not enough (00073), and the platform's own row is never suspended. Every change writes a trail
/// row on the ACTING platform person's account with the tenant's code as the detail, saved with the change.
/// </summary>
public abstract class TenantCommandHandlerBase
{
    private readonly IUserAccountRepository userAccounts;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;

    protected TenantCommandHandlerBase(ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform)
    {
        this.Tenants = tenants;
        this.userAccounts = userAccounts;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.CallContext = callContext;
        this.PlatformTenantId = platform.Value.TenantId;
    }

    protected ITenantRepository Tenants { get; }

    protected ICallContext CallContext { get; }

    protected Guid PlatformTenantId { get; }

    /// <summary>The caller's tenant is the platform's, or the door refuses (00073). The token's tenant is the wall.</summary>
    protected bool CallerIsPlatform
        => Guid.TryParse(this.CallContext.TenantId(), out var tenant) && tenant == this.PlatformTenantId;

    protected bool IsPlatformTenant(Guid id) => id == this.PlatformTenantId;

    /// <summary>Stages the trail row on the caller's own account; the handler's save stores it with the change.</summary>
    protected async Task TrailAsync(string action, Domain.Entities.Aggregates.TenantModule.Tenant tenant, CancellationToken cancellationToken)
    {
        var caller = await this.userAccounts.FindByIdAcrossTenantsAsync(RankRules.CallerId(this.CallContext), cancellationToken)
            ?? throw new InvalidOperationException("The caller's account cannot be found for the trail.");

        await this.auditEvents.AddAsync(this.securityEvents.Create(caller, action, tenant.Code), cancellationToken);
    }

    protected static CallResponse Ok()
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);

    protected static CallResponse Fail(string errorCode)
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));

    protected static CallResponse<T> Fail<T>(string errorCode)
        => CallResponseBuilder.CreateResponse<T>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));
}
