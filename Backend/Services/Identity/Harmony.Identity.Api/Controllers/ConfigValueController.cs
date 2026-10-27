using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Handler.Commands.ConfigModule.Values;
using Harmony.Identity.Handler.Queries.ConfigModule;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// A customer's setting values (design 5.1): set and cleared with ManageSettings for the whole customer, a group, or one
/// hotel; read with ViewSettings. Which value wins at a hotel, and why, is the effective door: any signed-in person who
/// may act at the hotel the call names reads it (a service reads its settings through the person's call).
/// </summary>
public class ConfigValueController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>A value at a place, new (201) or replacing the one there (200), with the row's id.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageSettings)]
    public async Task<IActionResult> Set([FromBody] SetConfigValueCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The value at a place goes; the next lower place wins again.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageSettings)]
    public async Task<IActionResult> Clear([FromBody] ClearConfigValueCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller's own customer's values, paged.</summary>
    [HttpPost("mine")]
    [ProducesResponseType(typeof(PagedResult<ConfigValueModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewSettings)]
    public async Task<IActionResult> GetMine([FromBody] GetMyConfigValuesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Every active key (or one key, or one service's keys) resolved at the hotel the call names, with the trace.</summary>
    [HttpGet("effective")]
    [ProducesResponseType(typeof(List<EffectiveSettingModel>), (int)HttpStatusCode.OK)]
    [AuthenticatedOnly]
    [RequireProperty]
    public async Task<IActionResult> Effective([FromQuery] string? key, [FromQuery] string? ownerService)
    {
        var response = await this.Mediator.Send(new GetEffectiveSettingsQuery { Key = key, OwnerService = ownerService });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
