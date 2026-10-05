using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule.Countries.Create;
using Harmony.Identity.Handler.Commands.ReferenceModule.Countries.Edit;
using Harmony.Identity.Handler.Queries.ReferenceModule.Countries.Detail;
using Harmony.Identity.Handler.Queries.ReferenceModule.Countries.List;

namespace Harmony.Identity.Api.Controllers;

/// <summary>Reference data: countries (ISO 3166-1). Platform data shared by every tenant: read with ViewTenants, written with ManageTenants. No delete: deactivate instead.</summary>
public class CountryController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] CreateCountryCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Update([FromBody] EditCountryCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<CountryModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedCountriesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(CountryModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var response = await this.Mediator.Send(new GetCountryByCodeQuery { Code = code });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
