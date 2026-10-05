using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule.RegulatoryEnvironments.Create;
using Harmony.Identity.Handler.Commands.ReferenceModule.RegulatoryEnvironments.Edit;
using Harmony.Identity.Handler.Queries.ReferenceModule.RegulatoryEnvironments.Detail;
using Harmony.Identity.Handler.Queries.ReferenceModule.RegulatoryEnvironments.List;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// Reference data: the regulatory environments (legal, tax, accounting, accommodation regimes per country).
/// Platform data shared by every tenant: read with ViewTenants, written with ManageTenants. No delete: other tables point
/// at the code, and a regime that stops applying is a fact for the services that use it, not a row to remove.
/// </summary>
public class RegulatoryEnvironmentController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] CreateRegulatoryEnvironmentCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Update([FromBody] EditRegulatoryEnvironmentCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<RegulatoryEnvironmentModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedRegulatoryEnvironmentsQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(RegulatoryEnvironmentModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var response = await this.Mediator.Send(new GetRegulatoryEnvironmentByCodeQuery { Code = code });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
