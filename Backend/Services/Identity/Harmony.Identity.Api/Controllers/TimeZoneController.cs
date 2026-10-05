using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule.TimeZones.Create;
using Harmony.Identity.Handler.Commands.ReferenceModule.TimeZones.Edit;
using Harmony.Identity.Handler.Queries.ReferenceModule.TimeZones.Detail;
using Harmony.Identity.Handler.Queries.ReferenceModule.TimeZones.List;

namespace Harmony.Identity.Api.Controllers;

/// <summary>Reference data: time zones (IANA). Read with ViewTenants, written with ManageTenants. No delete: deactivate instead.</summary>
public class TimeZoneController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] CreateTimeZoneCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Update([FromBody] EditTimeZoneCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<TimeZoneModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedTimeZonesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>An IANA id has a slash (Asia/Amman), so the route is a catch-all: GET api/TimeZone/Asia/Amman.</summary>
    [HttpGet("{*id}")]
    [ProducesResponseType(typeof(TimeZoneModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetById(string id)
    {
        var response = await this.Mediator.Send(new GetTimeZoneByIdQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
