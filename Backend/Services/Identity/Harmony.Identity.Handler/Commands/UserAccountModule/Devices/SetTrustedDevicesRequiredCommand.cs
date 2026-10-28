namespace Harmony.Identity.Handler.Commands.UserAccountModule.Devices;

using Harmony.Core.Abstractions;
using Harmony.Core.Errors;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.UserAccountModule.Roles;

/// <summary>The Mews gate (L16) on or off for the caller's customer: a sign-in from an untrusted browser waits for "Trust device".</summary>
public class SetTrustedDevicesRequiredCommand : BaseCommandRequest<CallResponse>
{
    public bool Required { get; set; }
}

public class SetTrustedDevicesRequiredCommandHandler : IRequestHandler<SetTrustedDevicesRequiredCommand, CallResponse>
{
    private readonly ITenantRepository tenants;
    private readonly IUserAccountRepository userAccounts;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly ICallContext callContext;

    public SetTrustedDevicesRequiredCommandHandler(ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext)
    {
        this.tenants = tenants;
        this.userAccounts = userAccounts;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.callContext = callContext;
    }

    public async Task<CallResponse> Handle(SetTrustedDevicesRequiredCommand command, CancellationToken cancellationToken)
    {
        var tenant = await this.tenants.FindAsync(Guid.Parse(this.callContext.TenantId()), cancellationToken);
        if (tenant is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.NotFound));
        }

        if (tenant.TrustedDevicesRequired == command.Required)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
        }

        tenant.RequireTrustedDevices(command.Required);
        if (await this.userAccounts.FindByIdAcrossTenantsAsync(RankRules.CallerId(this.callContext), cancellationToken) is { } caller)
        {
            await this.auditEvents.AddAsync(this.securityEvents.Create(caller, SecurityAuditActions.TrustedDevicesSet, command.Required ? "on" : "off"), cancellationToken);
        }

        await this.tenants.SaveChangesAsync(cancellationToken);
        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
