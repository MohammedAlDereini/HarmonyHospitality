using Harmony.Identity.Handler.Commands.UserAccountModule.Invitations;
using Harmony.Identity.Handler.Queries.UserAccountModule.SignInRecords;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// Invitations to Harmony's own sign-in (stage B, design 3.3): the customer's waiting ones are read with ViewUsers; an
/// administrator resends or cancels one with ManageUsers. Either way the link sent before dies.
/// </summary>
public class InvitationController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>The caller's customer's invitations still waiting (or expired), newest first.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(List<InvitationModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> GetMine()
    {
        var response = await this.Mediator.Send(new GetInvitationsQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>A fresh invitation for an account that has not chosen a password yet.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Resend([FromBody] ResendInvitationCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The waiting invitation is withdrawn; the account stays, without a password.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Cancel([FromBody] CancelInvitationCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
