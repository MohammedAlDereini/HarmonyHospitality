using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule.PropertyGroupTypes.Create;
using Harmony.Identity.Handler.Commands.ReferenceModule.PropertyGroupTypes.Edit;
using Harmony.Identity.Handler.Queries.ReferenceModule.PropertyGroupTypes.Detail;
using Harmony.Identity.Handler.Queries.ReferenceModule.PropertyGroupTypes.List;

namespace Harmony.Identity.Api.Controllers;

/// <summary>Reference data: property group types (Brand, Region, Owner, Custom...). Read with ViewTenants, written with ManageTenants. No delete.</summary>
public class PropertyGroupTypeController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] CreatePropertyGroupTypeCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Update([FromBody] EditPropertyGroupTypeCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<PropertyGroupTypeModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedPropertyGroupTypesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(PropertyGroupTypeModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var response = await this.Mediator.Send(new GetPropertyGroupTypeByCodeQuery { Code = code });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
