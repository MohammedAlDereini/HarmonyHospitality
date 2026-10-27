using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Handler.Commands.ConfigModule.Settings;
using Harmony.Identity.Handler.Queries.ConfigModule;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// The settings registry (design 5.1): the keys other services own, registered and changed by the platform tenant (us)
/// with ManageTenants (00073 from anywhere else). Read by the platform with ViewTenants and by a customer's people with
/// ViewSettings, so they know what they can set. No delete: a key is switched off, its values kept.
/// </summary>
public class ConfigSettingController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>A new key. 201 with the key.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Create([FromBody] RegisterConfigSettingCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The owner, the default, the narrowing, on or off; replaced whole. Never the key or the data type.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Update([FromBody] UpdateConfigSettingCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The keys, paged.</summary>
    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<ConfigSettingModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants, PermissionEnum.ViewSettings, Logic = PermissionLogic.Any)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedConfigSettingsQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>One key.</summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(ConfigSettingModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants, PermissionEnum.ViewSettings, Logic = PermissionLogic.Any)]
    public async Task<IActionResult> GetByKey(string key)
    {
        var response = await this.Mediator.Send(new GetConfigSettingByKeyQuery { Key = key });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
