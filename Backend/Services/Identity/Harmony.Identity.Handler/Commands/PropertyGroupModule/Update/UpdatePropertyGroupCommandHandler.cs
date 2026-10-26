namespace Harmony.Identity.Handler.Commands.PropertyGroupModule.Update;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>The platform's door: the group is found across tenants (00096 when unknown), takes the change, the trail is written, one save.</summary>
public class UpdatePropertyGroupCommandHandler : PropertyGroupCommandHandlerBase, IRequestHandler<UpdatePropertyGroupCommand, CallResponse>
{
    public UpdatePropertyGroupCommandHandler(
        ITenantRepository tenants,
        IPropertyGroupRepository groups,
        IPropertyGroupTypeRepository groupTypes,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform)
        : base(tenants, groups, groupTypes, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
    }

    public async Task<CallResponse> Handle(UpdatePropertyGroupCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var group = await this.Groups.FindAcrossTenantsAsync(command.Id, cancellationToken);
        if (group is null)
        {
            return Fail(BusinessErrorCodes.Identity.PropertyGroup.NotFound);
        }

        if (command.Brand is not null && !group.IsBrand)
        {
            return Fail(BusinessErrorCodes.Identity.PropertyGroup.BrandDetailNotAllowed);
        }

        group.Update(command.NameEn, command.NameAr, command.Description, new Precedence(command.Precedence), command.DisplayOrder, command.ExternalIdentifier, command.IsActive);
        group.SetBrandDetail(command.Brand?.ToDomain());
        await this.TrailAsync(SecurityAuditActions.PropertyGroupUpdated, group.Code, cancellationToken);
        await this.Groups.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
