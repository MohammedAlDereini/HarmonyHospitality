using Harmony.Identity.Handler.Commands.ApprovalModule;
using Harmony.Identity.Handler.Commands.UserAccountModule.Merge;
using Harmony.Identity.Handler.Queries.ApprovalModule;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// Approvals (stage D, design 4.2): when the customer requires them, risky changes wait here for a second administrator.
/// The person who asked never decides their own request. The merge of two accounts of one person lives here too.
/// </summary>
public class ApprovalController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>The customer's waiting requests, oldest first.</summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<ApprovalRequestModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> Pending()
    {
        var response = await this.Mediator.Send(new GetApprovalRequestsQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Runs the waiting change now.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Approve([FromBody] ApproveRequestCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Says no: nothing runs.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Reject([FromBody] RejectRequestCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Approvals on or off for the caller's customer (off goes through an approval while they are on).</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Required([FromBody] SetApprovalsRequiredCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Merges the second account into the first.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Merge([FromBody] MergeAccountsCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
