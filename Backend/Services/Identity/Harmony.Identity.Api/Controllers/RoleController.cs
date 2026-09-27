using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.RoleModule;
using Harmony.Identity.Handler.Commands.RoleModule.Create;
using Harmony.Identity.Handler.Commands.RoleModule.Delete;
using Harmony.Identity.Handler.Commands.RoleModule.Edit;
using Harmony.Identity.Handler.Commands.RoleModule.Permissions;
using Harmony.Identity.Handler.Queries.RoleModule.Detail;
using Harmony.Identity.Handler.Queries.RoleModule.List;
using Harmony.Identity.Handler.Queries.RoleModule.Permissions;

namespace Harmony.Identity.Api.Controllers;

public class RoleController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Created)]
    public async Task<IActionResult> Create([FromBody] CreateRoleCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Update([FromBody] EditRoleCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await this.Mediator.Send(new DeleteRoleCommand { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GrantPermission([FromBody] GrantRolePermissionCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> RevokePermission([FromBody] RevokeRolePermissionCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<RoleListModel>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedRolesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoleDetailModel), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await this.Mediator.Send(new GetRoleByIdQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("permissions")]
    [ProducesResponseType(typeof(List<string>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPermissions()
    {
        var response = await this.Mediator.Send(new GetPermissionCatalogQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}