using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Handler.Commands.ReferenceModule.Languages.Create;
using Harmony.Identity.Handler.Commands.ReferenceModule.Languages.Edit;
using Harmony.Identity.Handler.Queries.ReferenceModule.Languages.Detail;
using Harmony.Identity.Handler.Queries.ReferenceModule.Languages.List;

namespace Harmony.Identity.Api.Controllers;

/// <summary>Reference data: languages (ISO 639-1). Read with ViewTenants, written with ManageTenants. No delete: deactivate instead.</summary>
public class LanguageController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] CreateLanguageCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Update([FromBody] EditLanguageCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<LanguageModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedLanguagesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(LanguageModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var response = await this.Mediator.Send(new GetLanguageByCodeQuery { Code = code });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
