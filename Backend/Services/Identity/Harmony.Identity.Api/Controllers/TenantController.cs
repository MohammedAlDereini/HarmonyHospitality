using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.TenantModule;
using Harmony.Identity.Handler.Commands.TenantModule.Administrator;
using Harmony.Identity.Handler.Commands.TenantModule.Create;
using Harmony.Identity.Handler.Commands.TenantModule.Edit;
using Harmony.Identity.Handler.Commands.TenantModule.State;
using Harmony.Identity.Handler.Queries.TenantModule.Detail;
using Harmony.Identity.Handler.Queries.TenantModule.List;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// The customers, run from the platform console by the platform tenant (us): read with ViewTenants, written with
/// ManageTenants, and only from the platform tenant itself (00073 otherwise: the permission is never enough). Nothing
/// here reaches inside a customer: its people, roles and properties are its own.
/// </summary>
public class TenantController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>A new customer: Active from today, its code chosen once.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] CreateTenantCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>
    /// Send the tenant's administrator a fresh invitation while no password was chosen yet, making whatever of the birth is
    /// missing (catalogue, role, account). Refused once the administrator is active: from then on the tenant runs itself.
    /// </summary>
    [HttpPost("[action]")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> InviteAdministrator([FromBody] InviteTenantAdministratorCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Names, country, currency, tier and the billing reference; never the code or the status.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Update([FromBody] EditTenantCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Nobody in the customer signs in until it is reinstated; a reason is kept. Never the platform's own tenant.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Suspend([FromBody] SuspendTenantCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Reinstate([FromBody] ReinstateTenantCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<TenantModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedTenantsQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TenantModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await this.Mediator.Send(new GetTenantByIdQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
