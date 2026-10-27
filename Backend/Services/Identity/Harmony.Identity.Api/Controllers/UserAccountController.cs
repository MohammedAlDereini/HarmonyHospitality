using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Handler.Commands.UserAccountModule.Create;
using Harmony.Identity.Handler.Commands.UserAccountModule.Devices;
using Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;
using Harmony.Identity.Handler.Commands.UserAccountModule.Password;
using Harmony.Identity.Handler.Commands.UserAccountModule.Roles;
using Harmony.Identity.Handler.Commands.UserAccountModule.Sessions;
using Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;
using Harmony.Identity.Handler.Commands.UserAccountModule.TwoStepExemption;
using Harmony.Identity.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Harmony.Identity.Handler.Commands.UserAccountModule.State;
using Harmony.Identity.Handler.Queries.UserAccountModule.Detail;
using Harmony.Identity.Handler.Queries.UserAccountModule.Devices;
using Harmony.Identity.Handler.Queries.UserAccountModule.List;
using Harmony.Identity.Handler.Queries.UserAccountModule.Passkeys;
using Harmony.Identity.Handler.Queries.UserAccountModule.Permissions;
using Harmony.Identity.Handler.Queries.UserAccountModule.Security;

using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Handler.Commands.UserAccountModule.Access;
using Harmony.Identity.Handler.Queries.UserAccountModule.Access;

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

    /// <summary>An admin puts an account on the two-step exception list, with a reason: the password alone signs it in. Never a Manager or higher role, never the admin's own account.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> ExemptFromTwoStep([FromBody] ExemptFromTwoStepCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>An admin takes an account off the two-step exception list; every session of that account ends.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> EndTwoStepExemption([FromBody] EndTwoStepExemptionCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller's own security trail (sign-ins, password, two-step and session changes), newest 100 first.</summary>
    [HttpGet("[action]")]
    [ProducesResponseType(typeof(List<SecurityEventModel>), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> MySecurityEvents()
    {
        var response = await this.Mediator.Send(new GetSecurityEventsQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>
    /// The caller's own effective permissions: the values of every role the token names, the same set the permission
    /// check decides from. The web app shows and hides with it; it authorises nothing.
    /// </summary>
    [HttpGet("[action]")]
    [ProducesResponseType(typeof(List<int>), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> MyPermissions()
    {
        var response = await this.Mediator.Send(new GetMyPermissionsQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>An account's security trail, newest 100 first: who did what to it, from where, when.</summary>
    [HttpGet("{id:guid}/security-events")]
    [ProducesResponseType(typeof(List<SecurityEventModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> SecurityEvents(Guid id)
    {
        var response = await this.Mediator.Send(new GetSecurityEventsQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The browsers the caller signed in from, and whether each is still signed in.</summary>
    [HttpGet("[action]")]
    [ProducesResponseType(typeof(List<UserDeviceModel>), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> MyDevices()
    {
        var response = await this.Mediator.Send(new GetDevicesQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The browsers an account signed in from (Mews: "Devices and activity").</summary>
    [HttpGet("{id:guid}/devices")]
    [ProducesResponseType(typeof(List<UserDeviceModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> Devices(Guid id)
    {
        var response = await this.Mediator.Send(new GetDevicesQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller signs out of one of their devices; that device is forgotten.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> SignOutMyDevice([FromBody] SignOutMyDeviceCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>An admin signs a person out of one device; that device is forgotten.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> SignOutDevice([FromBody] SignOutDeviceCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Step 1 of adding a passkey for the caller: the WebAuthn creation options (the challenge), good for 5 minutes, once.</summary>
    /// <summary>Before a passkey is added or removed: the WebAuthn request options for one of the caller's own passkeys, to prove it is them.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(PasskeyRequestOptionsModel), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    [EnableRateLimiting(RateLimitPolicies.Passkeys)]
    public async Task<IActionResult> ConfirmIdentityOptions()
    {
        var response = await this.Mediator.Send(new ConfirmIdentityOptionsCommand());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller proves it is them with the password or a passkey answer; good for 5 minutes of passkey changes.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    [EnableRateLimiting(RateLimitPolicies.Passkeys)]
    public async Task<IActionResult> ConfirmIdentity([FromBody] ConfirmIdentityCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("[action]")]
    [ProducesResponseType(typeof(PasskeyCreationOptionsModel), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    [EnableRateLimiting(RateLimitPolicies.Passkeys)]
    public async Task<IActionResult> PasskeyOptions()
    {
        var response = await this.Mediator.Send(new StartPasskeyRegistrationCommand());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Step 2: the browser's answer and a name (or none: its maker's name); the passkey is stored and signs the caller in from now on.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(UserPasskeyModel), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    [EnableRateLimiting(RateLimitPolicies.Passkeys)]
    public async Task<IActionResult> AddPasskey([FromBody] AddPasskeyCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller's passkeys, newest first.</summary>
    [HttpGet("[action]")]
    [ProducesResponseType(typeof(List<UserPasskeyModel>), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> MyPasskeys()
    {
        var response = await this.Mediator.Send(new GetPasskeysQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>An account's passkeys (names and dates; never the keys).</summary>
    [HttpGet("{id:guid}/passkeys")]
    [ProducesResponseType(typeof(List<UserPasskeyModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> Passkeys(Guid id)
    {
        var response = await this.Mediator.Send(new GetPasskeysQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller renames one of their own passkeys; only the label changes.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> RenameMyPasskey([FromBody] RenameMyPasskeyCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller removes one of their own passkeys; every session of the account ends.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> RemoveMyPasskey([FromBody] RemoveMyPasskeyCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>An admin removes a passkey from an account (the rank rules apply); every session of the account ends and the person is told.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> RemovePasskey([FromBody] RemovePasskeyCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Anyone may ask. If exactly one active account has this e-mail, a one-time reset link goes to it. The answer is always 200.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.ForgotPassword)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Sets a new password with the token from a reset or invitation link; every existing session of the account ends.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.ResetPassword)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
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

    /// <summary>A role at one hotel or one group, open-ended or until a moment. 201 with the assignment id.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.AssignUsersToRole)]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Ends an assignment now; the row stays with its end.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.AssignUsersToRole)]
    public async Task<IActionResult> EndAssignment([FromBody] EndAssignmentCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>A temporary elevation at one hotel or group, for a reason, at most 30 days. 201 with the grant id.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.AssignUsersToRole)]
    public async Task<IActionResult> GrantAccess([FromBody] GrantAccessCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Revokes a grant now; the row stays with its revocation.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.AssignUsersToRole)]
    public async Task<IActionResult> RevokeGrant([FromBody] RevokeGrantCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>An account's assignments and grants, current first, with the role and the scope named.</summary>
    [HttpGet("{id:guid}/access")]
    [ProducesResponseType(typeof(AccountAccessModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> GetAccess(Guid id)
    {
        var response = await this.Mediator.Send(new GetAccountAccessQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller at one hotel: may they act there, and with which roles beyond the token's. What a service asks when it holds no copy.</summary>
    [HttpGet("MyAccessAt/{propertyId:guid}")]
    [ProducesResponseType(typeof(Harmony.Core.Identity.Permissions.HotelAccess), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    public async Task<IActionResult> MyAccessAt(Guid propertyId)
    {
        var response = await this.Mediator.Send(new GetMyAccessAtQuery { PropertyId = propertyId });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
