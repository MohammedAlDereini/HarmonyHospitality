using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Harmony.Core.Exceptions;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.DirectoryModule;
using Harmony.Identity.Handler.Commands.DirectoryModule.Scim;
using Harmony.Identity.Infrastructure.RateLimiting;
using Harmony.Identity.Infrastructure.Signing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Api.Controllers;

/// <summary>
/// Stage E: the SCIM 2.0 door (RFC 7644) a customer's Microsoft Entra ID or Okta pushes staff and groups to. Not a
/// Harmony token door: the directory proves itself with its own bearer value (see <see cref="ScimCallerAttribute"/>),
/// so the platform's token check stands aside ([AllowAnonymous]) and the customer comes from the connection, never from
/// the caller. Bodies and answers are application/scim+json; refusals use SCIM's error shape.
/// </summary>
[AllowAnonymous]
[ScimCaller]
[Route("scim/v2")]
[EnableRateLimiting(RateLimitPolicies.Directory)]
public class ScimController(IMediator mediator, IOptions<TokenIssuerSettings> issuer, ILogger<ScimController> logger) : ControllerBase
{
    private readonly string baseUrl = DirectoryCommandHandlerBase.ScimUrlOf(issuer.Value.Issuer.TrimEnd('/'));

    private DirectoryCaller Caller => (DirectoryCaller)this.HttpContext.Items[ScimCallerAttribute.ItemKey]!;

    [HttpGet("Users")]
    public Task<IActionResult> Users([FromQuery] string? filter, [FromQuery] int? startIndex, [FromQuery] int? count)
        => this.SendAsync(new ScimUsersQuery { Filter = filter, StartIndex = startIndex, Count = count });

    [HttpGet("Users/{id}")]
    public Task<IActionResult> UserById(string id) => this.SendAsync(new ScimUserQuery { Id = id });

    [HttpPost("Users")]
    public async Task<IActionResult> CreateUser()
        => await this.BodyAsync() is var (body, ok) && ok ? await this.SendAsync(new CreateScimUserCommand { Body = body }) : this.Malformed();

    [HttpPut("Users/{id}")]
    public async Task<IActionResult> ReplaceUser(string id)
        => await this.BodyAsync() is var (body, ok) && ok ? await this.SendAsync(new ReplaceScimUserCommand { Id = id, Body = body }) : this.Malformed();

    [HttpPatch("Users/{id}")]
    public async Task<IActionResult> PatchUser(string id)
        => await this.BodyAsync() is var (body, ok) && ok ? await this.SendAsync(new PatchScimUserCommand { Id = id, Body = body }) : this.Malformed();

    [HttpDelete("Users/{id}")]
    public Task<IActionResult> DeleteUser(string id) => this.SendAsync(new DeleteScimUserCommand { Id = id });

    [HttpGet("Groups")]
    public Task<IActionResult> Groups([FromQuery] string? filter, [FromQuery] int? startIndex, [FromQuery] int? count, [FromQuery] string? excludedAttributes)
        => this.SendAsync(new ScimGroupsQuery { Filter = filter, StartIndex = startIndex, Count = count, WithoutMembers = WithoutMembers(excludedAttributes) });

    [HttpGet("Groups/{id}")]
    public Task<IActionResult> GroupById(string id, [FromQuery] string? excludedAttributes)
        => this.SendAsync(new ScimGroupQuery { Id = id, WithoutMembers = WithoutMembers(excludedAttributes) });

    [HttpPost("Groups")]
    public async Task<IActionResult> CreateGroup()
        => await this.BodyAsync() is var (body, ok) && ok ? await this.SendAsync(new CreateScimGroupCommand { Body = body }) : this.Malformed();

    [HttpPut("Groups/{id}")]
    public async Task<IActionResult> ReplaceGroup(string id)
        => await this.BodyAsync() is var (body, ok) && ok ? await this.SendAsync(new ReplaceScimGroupCommand { Id = id, Body = body }) : this.Malformed();

    [HttpPatch("Groups/{id}")]
    public async Task<IActionResult> PatchGroup(string id)
        => await this.BodyAsync() is var (body, ok) && ok ? await this.SendAsync(new PatchScimGroupCommand { Id = id, Body = body }) : this.Malformed();

    [HttpDelete("Groups/{id}")]
    public Task<IActionResult> DeleteGroup(string id) => this.SendAsync(new DeleteScimGroupCommand { Id = id });

    /// <summary>What this door does and does not do (RFC 7643 5): PATCH and filters yes; bulk, sort, password, etag no.</summary>
    [HttpGet("ServiceProviderConfig")]
    public IActionResult ServiceProviderConfig() => Answer(ScimResult.Ok(new JsonObject
    {
        ["schemas"] = new JsonArray("urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig"),
        ["patch"] = new JsonObject { ["supported"] = true },
        ["bulk"] = new JsonObject { ["supported"] = false, ["maxOperations"] = 0, ["maxPayloadSize"] = 0 },
        ["filter"] = new JsonObject { ["supported"] = true, ["maxResults"] = ScimJson.MaxPageSize },
        ["changePassword"] = new JsonObject { ["supported"] = false },
        ["sort"] = new JsonObject { ["supported"] = false },
        ["etag"] = new JsonObject { ["supported"] = false },
        ["authenticationSchemes"] = new JsonArray(
            new JsonObject { ["type"] = "oauthbearertoken", ["name"] = "OAuth Bearer Token", ["description"] = "A one-hour token from the token door, or the connection's secret token.", ["primary"] = true }),
    }));

    private static bool WithoutMembers(string? excluded)
        => excluded?.Split(',').Any(a => a.Trim().Equals("members", StringComparison.OrdinalIgnoreCase)) == true;

    private async Task<IActionResult> SendAsync(ScimRequest request)
    {
        request.Caller = this.Caller;
        request.BaseUrl = this.baseUrl;
        try
        {
            var result = await mediator.Send(request, this.HttpContext.RequestAborted);
            if (result.Location is not null)
            {
                this.Response.Headers.Location = result.Location;
            }

            return Answer(result);
        }
        catch (Exception exception) when (exception is BusinessException or ArgumentException)
        {
            // A value Harmony's rules refuse (a name too long, an id that is not one): the directory reads SCIM's shape.
            logger.LogInformation(exception, "A directory's {Request} was refused as an invalid value.", request.GetType().Name);
            return Answer(ScimResult.Invalid("A value is not one Harmony accepts."));
        }
    }

    private async Task<(JsonObject? Body, bool Ok)> BodyAsync()
    {
        try
        {
            return (await JsonNode.ParseAsync(this.Request.Body, cancellationToken: this.HttpContext.RequestAborted) as JsonObject, true);
        }
        catch (JsonException)
        {
            return (null, false);
        }
    }

    private IActionResult Malformed() => Answer(ScimResult.Error(400, "The body is not JSON.", "invalidSyntax"));

    internal static IActionResult Answer(ScimResult result) => result.Body is null
        ? new StatusCodeResult(result.Status)
        : new ContentResult { StatusCode = result.Status, Content = result.Body.ToJsonString(), ContentType = "application/scim+json; charset=utf-8" };
}

/// <summary>
/// Stage E's token door (RFC 6749 4.4, client credentials): Microsoft Entra ID trades the connection's client id and
/// secret for a one-hour token. The secret goes straight to the checker, never through the command pipeline or its log.
/// </summary>
[AllowAnonymous]
[Route("scim/token")]
[EnableRateLimiting(RateLimitPolicies.Directory)]
public class ScimTokenController(IDirectoryCredentials credentials) : ControllerBase
{
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token()
    {
        // RFC 6749 5.1: a token answer is never stored.
        this.Response.Headers.CacheControl = "no-store";
        this.Response.Headers.Pragma = "no-cache";

        var form = await this.Request.ReadFormAsync(this.HttpContext.RequestAborted);
        if (!string.Equals(form["grant_type"], "client_credentials", StringComparison.Ordinal))
        {
            return Refuse(400, "unsupported_grant_type");
        }

        // client_secret_basic first (RFC 6749 2.3.1), else the form (client_secret_post).
        var (clientId, secret) = Basic(this.Request.Headers.Authorization.ToString()) ?? (form["client_id"].ToString(), form["client_secret"].ToString());
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret))
        {
            return Refuse(400, "invalid_request");
        }

        var token = await credentials.IssueAsync(clientId, secret, this.HttpContext.RequestAborted);
        if (token is null)
        {
            return Refuse(401, "invalid_client");
        }

        return new JsonResult(new { access_token = token.Value, token_type = "Bearer", expires_in = token.ExpiresInSeconds });
    }

    private static (string, string)? Basic(string header)
    {
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var pair = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
            var colon = pair.IndexOf(':');
            return colon > 0 ? (Uri.UnescapeDataString(pair[..colon]), Uri.UnescapeDataString(pair[(colon + 1)..])) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static IActionResult Refuse(int status, string error) => new JsonResult(new { error }) { StatusCode = status };
}

/// <summary>
/// The directory proves itself before any SCIM door runs: a bearer value the credentials accept, of a connection that is
/// on, of an active customer. Then the call runs inside that customer (the framework's wall reads the tenant from the
/// call). Anything else is 401 in SCIM's shape, with WWW-Authenticate.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ScimCallerAttribute : Attribute, IAsyncActionFilter
{
    public const string ItemKey = "harmony:directory-caller";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var header = http.Request.Headers.Authorization.ToString();
        var bearer = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
        // Checked in a scope of its own: the request's database context reads the customer once, when it is first made, so
        // it must not be made before the customer is set below.
        DirectoryCaller? caller = null;
        if (bearer is not null)
        {
            await using var check = http.RequestServices.CreateAsyncScope();
            caller = await check.ServiceProvider.GetRequiredService<IDirectoryCredentials>().AuthenticateAsync(bearer, http.RequestAborted);
        }

        if (caller is null)
        {
            http.Response.Headers.WWWAuthenticate = "Bearer realm=\"Harmony SCIM\"";
            context.Result = ScimController.Answer(ScimResult.Error(401, "The directory's token is missing, wrong, expired, or its connection is off."));
            return;
        }

        http.RequestServices.GetRequiredService<ICallContext>().SetTenantId(caller.TenantId.ToString("D"));
        http.Items[ItemKey] = caller;
        await next();
    }
}
