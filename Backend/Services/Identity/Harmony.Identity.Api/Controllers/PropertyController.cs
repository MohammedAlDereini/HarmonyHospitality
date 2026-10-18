using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.PropertyModule;
using Harmony.Identity.Handler.Commands.PropertyModule.Create;
using Harmony.Identity.Handler.Queries.PropertyModule.Detail;
using Harmony.Identity.Handler.Queries.PropertyModule.List;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// The hotels, run from the platform console by the platform tenant (us): read with ViewTenants, written with
/// ManageTenants, and only from the platform tenant itself (00073 otherwise: the permission is never enough). A hotel
/// is born with its first subscription lines; the hotel's own people get their own doors in step 3.
/// </summary>
public class PropertyController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>A new hotel of a customer, with its first subscription lines, in one save. 201 with the hotel's id.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] CreatePropertyCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The hotels, one customer's or everyone's, paged; the rows only, no lines.</summary>
    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<PropertyModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedPropertiesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>One hotel with its lines and the computed facts: held, licensed rooms, the rooms gap, open today.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PropertyModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await this.Mediator.Send(new GetPropertyByIdQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
