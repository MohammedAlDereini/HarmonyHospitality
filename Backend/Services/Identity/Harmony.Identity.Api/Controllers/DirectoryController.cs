using Harmony.Identity.Domain.Models.DirectoryModule;
using Harmony.Identity.Handler.Commands.DirectoryModule;
using Harmony.Identity.Handler.Queries.DirectoryModule;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// Stage E, the company directory (SCIM 2.0 from Microsoft Entra ID or Okta), the Mews way. The customer's administrator
/// opens the connection, gives the directory its address and secret, switches it on or off, and gives each group the
/// directory pushes a role (ManageUsers, AssignUsersToRole; read with ViewUsers). The platform removes a connection on
/// purpose (ManageTenants). The inactivity rule's number lives here too.
/// </summary>
public class DirectoryController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>The caller's own customer's connection and groups; null data when there is none.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(DirectoryModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> GetMine()
    {
        var response = await this.Mediator.Send(new GetMyDirectoryQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>A customer's connection and groups, for the console.</summary>
    [HttpGet("of/{tenantId:guid}")]
    [ProducesResponseType(typeof(DirectoryModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> OfTenant(Guid tenantId)
    {
        var response = await this.Mediator.Send(new GetDirectoryOfTenantQuery { TenantId = tenantId });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Opens the connection (201). The secret is in this answer and nowhere else, ever.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(DirectorySecretModel), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Open()
    {
        var response = await this.Mediator.Send(new OpenDirectoryConnectionCommand());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>A new secret: the old one, and every token made from it, stop working now.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(DirectorySecretModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> NewSecret()
    {
        var response = await this.Mediator.Send(new NewDirectorySecretCommand());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>On or off. Off refuses the directory; its people stay read-only.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Switch([FromBody] SwitchDirectoryCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The platform removes a customer's connection; its people go back to the customer's administrators.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Remove([FromBody] RemoveDirectoryConnectionCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The role a directory group gives, and where; its members' access follows at once.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.AssignUsersToRole)]
    public async Task<IActionResult> MapGroup([FromBody] MapDirectoryGroupCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Days without a sign-in before an account is suspended, for the caller's customer: 1 to 90.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> InactivityDays([FromBody] SetInactivityDaysCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The platform runs the nightly inactivity rule now; the answer is how many accounts it suspended.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> RunInactivityRule()
    {
        var response = await this.Mediator.Send(new RunInactivityRuleCommand());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
