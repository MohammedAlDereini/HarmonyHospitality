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
    [RequirePermission(PermissionEnum.CreateRole)]
    public async Task<IActionResult> Create([FromBody] CreateRoleCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.EditRole)]
    public async Task<IActionResult> Update([FromBody] EditRoleCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.DeleteRole)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await this.Mediator.Send(new DeleteRoleCommand { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Sets the role's permissions to exactly this list of ids (PropX AssignPermissions).</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageRolePermissions)]
    public async Task<IActionResult> UpdatePermissions([FromBody] UpdateRolePermissionsCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<RoleListModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewRoles)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedRolesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoleDetailModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewRoles)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await this.Mediator.Send(new GetRoleByIdQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The permission catalogue of the caller's tenant: what a role can be given.</summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(List<PermissionModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewRoles)]
    public async Task<IActionResult> GetPermissions()
    {
        var response = await this.Mediator.Send(new GetPermissionCatalogQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}