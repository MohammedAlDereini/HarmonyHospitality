using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.PropertyGroupModule;
using Harmony.Identity.Handler.Commands.PropertyGroupModule.Create;
using Harmony.Identity.Handler.Commands.PropertyGroupModule.Members;
using Harmony.Identity.Handler.Commands.PropertyGroupModule.Update;
using Harmony.Identity.Handler.Queries.PropertyGroupModule.Detail;
using Harmony.Identity.Handler.Queries.PropertyGroupModule.List;
using Harmony.Identity.Handler.Queries.PropertyGroupModule.Mine;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// The hotel groups (brands, regions, owners), run from the platform console by the platform tenant (us): read with
/// ViewTenants, written with ManageTenants, and only from the platform tenant itself (00073 otherwise). A hotel's own
/// people read their customer's groups with ViewProperties.
/// </summary>
public class PropertyGroupController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>A new group of a customer. 201 with the group's id.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] CreatePropertyGroupCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>What the platform may change about a group, replaced whole. Never the code or the type.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Update([FromBody] UpdatePropertyGroupCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>A hotel joins a group, from now or from a given moment. 201 with the membership id. A second brand is 00099.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> AddMember([FromBody] AddPropertyGroupMemberCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Closes a hotel's membership, now or at a given moment. A membership ends once.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> EndMembership([FromBody] EndPropertyGroupMembershipCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The groups, one customer's or everyone's, paged.</summary>
    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<PropertyGroupModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedPropertyGroupsQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>One group.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PropertyGroupModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await this.Mediator.Send(new GetPropertyGroupByIdQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller's own customer's groups, paged. Walled by the caller's tenant.</summary>
    [HttpPost("mine")]
    [ProducesResponseType(typeof(PagedResult<PropertyGroupModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewProperties)]
    public async Task<IActionResult> GetMine([FromBody] GetMyPropertyGroupsQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>One of the caller's own groups; another customer's group is 00096.</summary>
    [HttpGet("mine/{id:guid}")]
    [ProducesResponseType(typeof(PropertyGroupModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewProperties)]
    public async Task<IActionResult> GetMineById(Guid id)
    {
        var response = await this.Mediator.Send(new GetMyPropertyGroupByIdQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
