namespace Harmony.Identity.Handler.Commands.PropertyGroupModule.Members;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>Found across tenants (00100), still open (00102), the end not before the start (00104); then closed, trailed, one save.</summary>
public class EndPropertyGroupMembershipCommandHandler : PropertyGroupCommandHandlerBase, IRequestHandler<EndPropertyGroupMembershipCommand, CallResponse>
{
    private readonly IPropertyGroupMemberRepository members;
    private readonly IPropertyRepository properties;
    private readonly TimeProvider clock;

    public EndPropertyGroupMembershipCommandHandler(
        ITenantRepository tenants,
        IPropertyGroupRepository groups,
        IPropertyGroupTypeRepository groupTypes,
        IPropertyGroupMemberRepository members,
        IPropertyRepository properties,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        TimeProvider clock)
        : base(tenants, groups, groupTypes, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.members = members;
        this.properties = properties;
        this.clock = clock;
    }

    public async Task<CallResponse> Handle(EndPropertyGroupMembershipCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var member = await this.members.FindAcrossTenantsAsync(command.MemberId, cancellationToken);
        if (member is null)
        {
            return Fail(BusinessErrorCodes.Identity.PropertyGroup.MemberNotFound);
        }

        if (!member.IsCurrent)
        {
            return Fail(BusinessErrorCodes.Identity.PropertyGroup.MembershipEnded);
        }

        var end = command.ToUtc ?? this.clock.GetUtcNow();
        if (end < member.Period.FromUtc)
        {
            return Fail(BusinessErrorCodes.Identity.PropertyGroup.EndsBeforeStart);
        }

        var groupCode = await this.Groups.QueryAcrossTenants().Where(g => g.Id == member.PropertyGroupId).Select(g => g.Code).FirstAsync(cancellationToken);
        var hotelCode = await this.properties.QueryAcrossTenants().Where(p => p.Id == member.PropertyId).Select(p => p.Code).FirstAsync(cancellationToken);

        member.End(end);
        await this.TrailAsync(SecurityAuditActions.PropertyGroupMemberEnded, $"{groupCode}:{hotelCode}", cancellationToken);
        await this.members.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
