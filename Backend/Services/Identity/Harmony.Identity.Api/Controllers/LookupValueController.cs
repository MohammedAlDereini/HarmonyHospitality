using Harmony.Identity.Handler.Commands.LookupModule.Values.Create;
using Harmony.Identity.Handler.Commands.LookupModule.Values.Delete;
using Harmony.Identity.Handler.Commands.LookupModule.Values.Edit;
using Harmony.Identity.Handler.Queries.LookupModule.Values.List;

namespace Harmony.Identity.Api.Controllers;

public class LookupValueController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.Created)]
    public async Task<IActionResult> Create([FromBody] CreateLookupValueCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Update([FromBody] EditLookupValueCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await this.Mediator.Send(new DeleteLookupValueCommand { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedLookupValuesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}