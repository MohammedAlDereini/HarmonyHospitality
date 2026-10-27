namespace Harmony.Identity.Handler.Commands.PropertyGroupModule.Members;

using Harmony.Core.Abstractions;
using Harmony.Core.Exceptions;
using Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// The hotel must be the group's customer's (00092, no hint), not already in it (00101); for an exclusive type it
/// must not be in another current group of that type (00099: asked first, and for Brand the filtered index answers
/// the race). An inactive group takes nobody (00103). One save: the row and the trail.
/// </summary>
public class AddPropertyGroupMemberCommandHandler : PropertyGroupCommandHandlerBase, IRequestHandler<AddPropertyGroupMemberCommand, CallResponse<Guid>>
{
    private readonly IPropertyGroupMemberRepository members;
    private readonly IPropertyRepository properties;
    private readonly TimeProvider clock;
    private readonly ICacheService cache;

    public AddPropertyGroupMemberCommandHandler(
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
        TimeProvider clock,
        ICacheService cache)
        : base(tenants, groups, groupTypes, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.cache = cache;
        this.members = members;
        this.properties = properties;
        this.clock = clock;
    }

    public async Task<CallResponse<Guid>> Handle(AddPropertyGroupMemberCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var group = await this.Groups.FindAcrossTenantsAsync(command.PropertyGroupId, cancellationToken);
        if (group is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.PropertyGroup.NotFound);
        }

        if (!group.IsActive)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.PropertyGroup.NotActive);
        }

        var hotelCode = await this.properties.QueryAcrossTenants()
            .Where(p => p.Id == command.PropertyId && p.TenantId == group.TenantId)
            .Select(p => p.Code)
            .FirstOrDefaultAsync(cancellationToken);
        if (hotelCode is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Property.NotFound);
        }

        if (await this.members.IsCurrentMemberAsync(group.Id, command.PropertyId, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.PropertyGroup.AlreadyMember);
        }

        var type = await this.GroupTypes.GetByCodeAsync(group.TypeCode, cancellationToken);
        if (type is { IsExclusive: true } && await this.members.HasCurrentGroupOfTypeAsync(group.TenantId, command.PropertyId, group.TypeCode, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.PropertyGroup.OneBrandPerHotel);
        }

        var member = PropertyGroupMember.Join(group, command.PropertyId, command.FromUtc ?? this.clock.GetUtcNow());
        await this.members.AddAsync(member, cancellationToken);
        await this.TrailAsync(SecurityAuditActions.PropertyGroupMemberAdded, $"{group.Code}:{hotelCode}", cancellationToken);

        try
        {
            await this.members.SaveChangesAsync(cancellationToken);
        }
        catch (BusinessException exception) when (exception.Errors.Any(error => error.Code == ConflictCode))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.PropertyGroup.OneBrandPerHotel);
        }

        // A hotel joined a group: every hotel copy of the customer is stale (group roles reach it now).
        await this.cache.DeleteByPrefixAsync(Harmony.Core.Identity.Permissions.PermissionCacheKeys.HotelRolePrefixOf(group.TenantId), cancellationToken);
        await this.cache.DeleteByPrefixAsync(Harmony.Core.Identity.Settings.SettingsCacheKeys.HotelSettingsPrefixOf(group.TenantId), cancellationToken);
        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(member.Id);
    }
}
