using Harmony.Identity.Handler.Commands.LookupModule.Categories.Create;
using Harmony.Identity.Handler.Commands.LookupModule.Categories.Delete;
using Harmony.Identity.Handler.Commands.LookupModule.Categories.Edit;
using Harmony.Identity.Handler.Queries.LookupModule.LookupCategories.List;

namespace Harmony.Identity.Api.Controllers;

public class LookupCategoryController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.Created)]
    public async Task<IActionResult> Create([FromBody] CreateLookupCategoryCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Update([FromBody] EditLookupCategoryCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await this.Mediator.Send(new DeleteLookupCategoryCommand { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedLookupCategoriesQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}