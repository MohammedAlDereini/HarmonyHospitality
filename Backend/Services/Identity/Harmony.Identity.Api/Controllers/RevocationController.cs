using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Handler.Commands.RevocationModule;
using Harmony.Identity.Handler.Queries.RevocationModule;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// The revocation feed (stage C, design 4.1): who lost access, in order, per customer. Platform only. A reader keeps its
/// position, reads on from it, and loads the snapshot when the feed says lines it needed were pruned.
/// </summary>
public class RevocationController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>A page of the customer's feed after a position, oldest first.</summary>
    [HttpGet("feed")]
    [ProducesResponseType(typeof(RevocationFeedModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> Feed([FromQuery] GetRevocationFeedQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>A page of the customer's newest snapshot.</summary>
    [HttpGet("snapshot")]
    [ProducesResponseType(typeof(RevocationSnapshotModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> Snapshot([FromQuery] GetRevocationSnapshotQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Compacts the customer's feed now (the hourly job does it on its own).</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(RevocationCompaction), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Compact([FromBody] CompactRevocationFeedCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
