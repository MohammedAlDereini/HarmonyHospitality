using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule.CountrySubdivisions.Create;
using Harmony.Identity.Handler.Commands.ReferenceModule.CountrySubdivisions.Edit;
using Harmony.Identity.Handler.Queries.ReferenceModule.CountrySubdivisions.Detail;
using Harmony.Identity.Handler.Queries.ReferenceModule.CountrySubdivisions.List;

namespace Harmony.Identity.Api.Controllers;

/// <summary>Reference data: country subdivisions (ISO 3166-2). Read with ViewTenants, written with ManageTenants. No delete.</summary>
public class CountrySubdivisionController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] CreateCountrySubdivisionCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Update([FromBody] EditCountrySubdivisionCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<CountrySubdivisionModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedCountrySubdivisionsQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(CountrySubdivisionModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var response = await this.Mediator.Send(new GetCountrySubdivisionByCodeQuery { Code = code });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
