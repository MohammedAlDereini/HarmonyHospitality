using Harmony.Core.Models;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Handler.Commands.UserAccountModule.ServicePrincipals;
using Harmony.Identity.Handler.Commands.UserAccountModule.State;
using Harmony.Identity.Handler.Queries.UserAccountModule.Detail;
using Harmony.Identity.Handler.Queries.UserAccountModule.List;

namespace Harmony.Identity.Api.Controllers;

public class UserAccountController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>Creates a machine account. The response is the only place the secret ever appears.</summary>
    [HttpPost("service-principals")]
    [ProducesResponseType(typeof(ServicePrincipalCreatedModel), (int)HttpStatusCode.Created)]
    public async Task<IActionResult> CreateServicePrincipal([FromBody] CreateServicePrincipalCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>New secret; the old one stops working at once.</summary>
    [HttpPost("{id:guid}/rotate-secret")]
    [ProducesResponseType(typeof(ServicePrincipalCreatedModel), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> RotateSecret(Guid id)
    {
        var response = await this.Mediator.Send(new RotateServiceCredentialCommand { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Suspend([FromBody] SuspendUserAccountCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Reinstate([FromBody] ReinstateUserAccountCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Terminate([FromBody] TerminateUserAccountCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpPost("get-all")]
    [ProducesResponseType(typeof(PagedResult<UserAccountModel>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetAllPaged([FromBody] GetPagedUserAccountsQuery query)
    {
        var response = await this.Mediator.Send(query);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserAccountModel), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await this.Mediator.Send(new GetUserAccountByIdQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
