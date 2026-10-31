using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.SecurityAuditModule;
using Harmony.Identity.Handler.Queries.SecurityAuditModule;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// The platform's security trail across every customer, for the console's Audit log and Sign-ins: read with ViewTenants,
/// and only from the platform tenant itself (00073 otherwise: the permission is never enough). Read only: the trail is
/// written by the security journal and never changed by hand. A customer reads its own people's trail at
/// UserAccount/{id}/security-events, never here.
/// </summary>
public class SecurityAuditController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>The trail, newest first, with the customer's and the person's names; every filter optional.</summary>
    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<PlatformTrailEventModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedPlatformTrailQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>How often each action was written inside a window (the last 24 hours when none is given).</summary>
    [HttpGet("counts")]
    [ProducesResponseType(typeof(List<TrailCountModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> Counts([FromQuery] GetPlatformTrailCountsQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
