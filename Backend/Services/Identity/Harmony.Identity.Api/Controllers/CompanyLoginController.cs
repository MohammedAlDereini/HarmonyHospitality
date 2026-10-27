using Harmony.Identity.Domain.Models.CompanyLoginModule;
using Harmony.Identity.Handler.Commands.CompanyLoginModule.Add;
using Harmony.Identity.Handler.Commands.CompanyLoginModule.Domains;
using Harmony.Identity.Handler.Commands.CompanyLoginModule.People;
using Harmony.Identity.Handler.Commands.CompanyLoginModule.Switch;
using Harmony.Identity.Handler.Queries.CompanyLoginModule;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// Company login (stage A, the Mews way). The platform adds a customer's company login (ManageTenants). The customer's
/// administrator lists the company's e-mail domains, switches it on, and decides who keeps a Harmony password
/// (ManageUsers); the people and the setup are read with ViewUsers. Staff only sign in.
/// </summary>
public class CompanyLoginController(IMediator mediator, ICallResponseManager callResponseManager) : BaseController(mediator, callResponseManager)
{
    /// <summary>The platform adds Microsoft or Google to a customer, with one of the company's domains (201 with its id). It starts off.</summary>
    [HttpPost("[action]")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Created)]
    [RequirePermission(PermissionEnum.ManageTenants)]
    public async Task<IActionResult> Add([FromBody] AddCompanyLoginCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>A customer's company logins, for the console.</summary>
    [HttpGet("of/{tenantId:guid}")]
    [ProducesResponseType(typeof(List<CompanyLoginModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewTenants)]
    public async Task<IActionResult> OfTenant(Guid tenantId)
    {
        var response = await this.Mediator.Send(new GetCompanyLoginsOfTenantQuery { TenantId = tenantId });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>The caller's own customer's company logins.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(List<CompanyLoginModel>), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> GetMine()
    {
        var response = await this.Mediator.Send(new GetMyCompanyLoginsQuery());
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>One more of the company's e-mail domains.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> AddDomain([FromBody] AddCompanyLoginDomainCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>An e-mail domain off the list.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> RemoveDomain([FromBody] RemoveCompanyLoginDomainCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>On or off.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> Switch([FromBody] SwitchCompanyLoginCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>How one person signs in: the administrator's choice, what covers them, their company links.</summary>
    [HttpGet("account/{id:guid}")]
    [ProducesResponseType(typeof(AccountSignInModel), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ViewUsers)]
    public async Task<IActionResult> Account(Guid id)
    {
        var response = await this.Mediator.Send(new GetAccountSignInQuery { Id = id });
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>Keep a person on a Harmony password (true), or give them to the company login (false).</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> PasswordSignIn([FromBody] SetPasswordSignInCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }

    /// <summary>End one company link of a person; their sessions end with it.</summary>
    [HttpPut("[action]")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [RequirePermission(PermissionEnum.ManageUsers)]
    public async Task<IActionResult> EndLink([FromBody] EndCompanyLinkCommand command)
    {
        var response = await this.Mediator.Send(command);
        return await this.CallResponseManager.AsActionResultAsync(response);
    }
}
