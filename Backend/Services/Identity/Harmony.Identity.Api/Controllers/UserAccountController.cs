using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Handler.Commands.UserAccountModule.Create;
using Harmony.Identity.Handler.Commands.UserAccountModule.Password;
using Harmony.Identity.Handler.Commands.UserAccountModule.Roles;
using Harmony.Identity.Handler.Commands.UserAccountModule.Sessions;
using Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;
using Harmony.Identity.Handler.Commands.UserAccountModule.State;
using Harmony.Identity.Handler.Queries.UserAccountModule.Detail;
using Harmony.Identity.Handler.Queries.UserAccountModule.List;

namespace Harmony.Identity.Api.Controllers;

public class UserAccountController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>An admin creates an account with its roles and an initial password that must be changed at first login.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Create([FromBody] CreateUserAccountCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Suspend([FromBody] SuspendUserAccountCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Reinstate([FromBody] ReinstateUserAccountCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Terminate([FromBody] TerminateUserAccountCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<UserAccountModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedUserAccountsQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserAccountModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await this.Mediator.Send(new GetUserAccountByIdQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller replaces their own password. The one call a must-change-password token may make.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Ends every session of the caller on every device: the version bumps and the refresh tokens go. The person signs in again once.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> LogoutEverywhere()
    {
        var response = await this.Mediator.Send(new LogoutEverywhereCommand());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Step 1 of turning on an authenticator app: the shared key and the otpauth URI (QR) for the caller. Nothing is enabled yet.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(TwoFactorSetupModel), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> SetupTwoFactor()
    {
        var response = await this.Mediator.Send(new SetupTwoFactorCommand());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Step 2: a code from the app proves the setup; two-factor turns on and the recovery codes are shown once.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(TwoFactorRecoveryCodesModel), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> EnableTwoFactor([FromBody] EnableTwoFactorCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller turns two-factor off; the current password is required.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>An admin removes a lost authenticator from an account; every session of that account ends.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> ResetTwoFactor([FromBody] ResetTwoFactorCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Sets the account's roles to exactly this list (id + roleIds in the body, like Suspend). The next token carries their ids.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.AssignUsersToRole)]
    public async Task<IActionResult> UpdateRoles([FromBody] UpdateUserRolesCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
