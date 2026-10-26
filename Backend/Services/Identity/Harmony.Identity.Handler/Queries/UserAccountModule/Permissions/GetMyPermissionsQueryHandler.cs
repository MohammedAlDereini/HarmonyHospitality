using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Identity.Domain.Repositories;
using Harmony.Core.Identity.Permissions;
using Microsoft.AspNetCore.Http;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Permissions;

/// <summary>
/// The union of the permission values of every role the caller's token names, from the same resolver
/// [RequirePermission] decides with, so the web app hides exactly what the API would refuse. A caller who must change
/// the password first holds nothing yet, as the permission check also says. Sorted, no duplicates, never a guess:
/// an unreadable role answers nothing.
/// </summary>
public class GetMyPermissionsQueryHandler : IRequestHandler<GetMyPermissionsQuery, CallResponse<List<int>>>
{
    private readonly IHttpContextAccessor http;
    private readonly IPlatformPermissionResolver permissions;
    private readonly ICallContext callContext;
    private readonly IUserAccountRepository userAccounts;
    private readonly TimeProvider clock;

    public GetMyPermissionsQueryHandler(IHttpContextAccessor http, IPlatformPermissionResolver permissions, ICallContext callContext, IUserAccountRepository userAccounts, TimeProvider clock)
    {
        this.http = http;
        this.permissions = permissions;
        this.callContext = callContext;
        this.userAccounts = userAccounts;
        this.clock = clock;
    }

    public async Task<CallResponse<List<int>>> Handle(GetMyPermissionsQuery request, CancellationToken cancellationToken)
    {
        var user = this.http.HttpContext?.User;
        var effective = new HashSet<int>();

        if (user?.Identity is { IsAuthenticated: true }
            && Guid.TryParse(user.FindFirst(PlatformClaimTypes.Tenant)?.Value, out var tenantId)
            && tenantId != Guid.Empty
            && !user.HasClaim(claim => claim.Type == PlatformClaimTypes.MustChangePassword))
        {
            // A role claim that is not a GUID is ignored, exactly as the permission check ignores it.
            var roleIds = user.FindAll(PlatformClaimTypes.Role)
                .Select(claim => Guid.TryParse(claim.Value, out var roleId) ? roleId : Guid.Empty)
                .Where(roleId => roleId != Guid.Empty)
                .Distinct();

            foreach (var roleId in roleIds)
            {
                effective.UnionWith(await this.permissions.GetPermissionsAsync(tenantId, roleId, cancellationToken));
            }

            // The hotel the call is about adds the roles that reach it: assignments and grants, direct or through a group.
            if (Guid.TryParse(this.callContext.PropertyId(), out var propertyId) && propertyId != Guid.Empty
                && Guid.TryParse(user.FindFirst(PlatformClaimTypes.Subject)?.Value, out var userId))
            {
                foreach (var roleId in await this.userAccounts.RoleIdsAtHotelAsync(userId, propertyId, this.clock.GetUtcNow(), cancellationToken))
                {
                    effective.UnionWith(await this.permissions.GetPermissionsAsync(tenantId, roleId, cancellationToken));
                }
            }
        }

        return CallResponseBuilder.CreateResponse<List<int>>(eCallResponseStatus.Success).HasData(effective.Order().ToList());
    }
}
