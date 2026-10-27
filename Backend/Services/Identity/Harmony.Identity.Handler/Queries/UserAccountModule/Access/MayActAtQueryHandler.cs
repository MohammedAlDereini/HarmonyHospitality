using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Access;

/// <summary>
/// The rule in one place: the hotel is the caller's customer's (the platform's people pass for every hotel they run);
/// then a tenant-wide role, a current assignment, or a live grant that reaches the hotel. Every "no" is a trail row on
/// the person's own account (access.property-refused, the hotel id), saved here: a refusal is a security event.
/// </summary>
public class MayActAtQueryHandler : IRequestHandler<MayActAtQuery, CallResponse<bool>>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IPropertyRepository properties;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly TimeProvider clock;
    private readonly ICacheService cache;
    private readonly Guid platformTenantId;

    public MayActAtQueryHandler(IUserAccountRepository userAccounts, IPropertyRepository properties, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, TimeProvider clock, ICacheService cache, IOptions<PlatformTenantSettings> platform)
    {
        this.cache = cache;
        this.userAccounts = userAccounts;
        this.properties = properties;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.clock = clock;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<bool>> Handle(MayActAtQuery request, CancellationToken cancellationToken)
    {
        var hotelTenant = await this.properties.QueryAcrossTenants().Where(p => p.Id == request.PropertyId).Select(p => (Guid?)p.TenantId).FirstOrDefaultAsync(cancellationToken);
        var roleIds = hotelTenant == request.TenantId
            ? await this.userAccounts.RoleIdsAtHotelAsync(request.UserId, request.PropertyId, this.clock.GetUtcNow(), cancellationToken)
            : [];
        var allowed = (hotelTenant is not null && request.TenantId == this.platformTenantId)
            || (hotelTenant == request.TenantId && (roleIds.Count > 0 || await this.userAccounts.HoldsAnyTenantWideRoleAsync(request.UserId, cancellationToken)));
        if (allowed)
        {
            // The copy every service reads: its presence is this "yes", its roles are what the hotel adds to the token's.
            await this.cache.AddAsync(
                PermissionCacheKeys.HotelRoleKey(request.TenantId, request.UserId, request.PropertyId),
                new HotelAccess { Allowed = true, RoleIds = roleIds.ToList() },
                PermissionCacheKeys.HotelRolesLifetime,
                cancellationToken);
            return Answer(true);
        }

        var person = await this.userAccounts.FindByIdAcrossTenantsAsync(request.UserId, cancellationToken);
        if (person is not null)
        {
            await this.auditEvents.AddAsync(this.securityEvents.Create(person, SecurityAuditActions.PropertyAccessRefused, request.PropertyId.ToString()), cancellationToken);
            await this.auditEvents.SaveChangesAsync(cancellationToken);
        }

        return Answer(false);
    }

    private static CallResponse<bool> Answer(bool value) => CallResponseBuilder.CreateResponse<bool>(eCallResponseStatus.Success).HasData(value);
}
