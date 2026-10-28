namespace Harmony.Identity.Handler.Commands.DirectoryModule.Scim;

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

public sealed class ScimUsersQuery : ScimRequest
{
    public string? Filter { get; set; }

    public int? StartIndex { get; set; }

    public int? Count { get; set; }
}

public sealed class ScimUserQuery : ScimRequest
{
    public string Id { get; set; } = null!;
}

public sealed class CreateScimUserCommand : ScimRequest
{
    public JsonObject? Body { get; set; }
}

public sealed class ReplaceScimUserCommand : ScimRequest
{
    public string Id { get; set; } = null!;

    public JsonObject? Body { get; set; }
}

public sealed class PatchScimUserCommand : ScimRequest
{
    public string Id { get; set; } = null!;

    public JsonObject? Body { get; set; }
}

public sealed class DeleteScimUserCommand : ScimRequest
{
    public string Id { get; set; } = null!;
}

/// <summary>
/// SCIM Users (RFC 7644) for Microsoft Entra ID and Okta. The directory sees only its own people, never Harmony's
/// others and never a deleted one. A new person waits with no access until a group with a role takes them in. A person
/// Harmony already has (same e-mail, same customer) is taken over; an e-mail of another customer is refused (409), as in
/// Mews. The e-mail (userName) is the person's key and never changes through SCIM. active=false suspends at once (every
/// session and token dies); active=true turns them back on with the access today's groups give; DELETE terminates and
/// frees the e-mail, keeping the row and its history (RFC 7644 3.6). Attributes Harmony does not keep are ignored.
/// </summary>
public sealed partial class ScimUserHandlers :
    IRequestHandler<ScimUsersQuery, ScimResult>,
    IRequestHandler<ScimUserQuery, ScimResult>,
    IRequestHandler<CreateScimUserCommand, ScimResult>,
    IRequestHandler<ReplaceScimUserCommand, ScimResult>,
    IRequestHandler<PatchScimUserCommand, ScimResult>,
    IRequestHandler<DeleteScimUserCommand, ScimResult>
{
    private const string TurnedOff = "Turned off in the company directory.";
    private const string Deleted = "Deleted in the company directory.";

    private readonly UserManager<User> userManager;
    private readonly IUserAccountRepository userAccounts;
    private readonly IDirectoryGroupRepository groups;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly DirectoryAccess access;
    private readonly ILogger<ScimUserHandlers> logger;

    public ScimUserHandlers(UserManager<User> userManager, IUserAccountRepository userAccounts, IDirectoryGroupRepository groups, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, DirectoryAccess access, ILogger<ScimUserHandlers> logger)
    {
        this.userManager = userManager;
        this.userAccounts = userAccounts;
        this.groups = groups;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.access = access;
        this.logger = logger;
    }

    public async Task<ScimResult> Handle(ScimUsersQuery request, CancellationToken cancellationToken)
    {
        if (!ScimFilter.TryParse(request.Filter, out var terms))
        {
            return ScimResult.Error(400, "The filter is not one Harmony reads: eq terms joined by and.", "invalidFilter");
        }

        var people = this.Visible(request.Caller);
        foreach (var (path, value) in terms)
        {
            switch (path)
            {
                case "username":
                    var name = this.userManager.NormalizeName(value);
                    people = people.Where(u => u.NormalizedUserName == name);
                    break;
                case "externalid":
                    people = people.Where(u => u.DirectoryExternalId == value);
                    break;
                case "emails.value":
                case "emails":
                    var email = this.userManager.NormalizeEmail(value);
                    people = people.Where(u => u.NormalizedEmail == email);
                    break;
                case "emails.type":
                    people = string.Equals(value, "work", StringComparison.OrdinalIgnoreCase) ? people : people.Where(_ => false);
                    break;
                case "id":
                    people = Guid.TryParse(value, out var id) ? people.Where(u => u.Id == id) : people.Where(_ => false);
                    break;
                case "displayname":
                    people = people.Where(u => u.DisplayName == value);
                    break;
                default:
                    return ScimResult.Error(400, $"Harmony cannot filter users by {path}.", "invalidFilter");
            }
        }

        var (skip, take, start) = ScimJson.Page(request.StartIndex, request.Count);
        var total = await people.CountAsync(cancellationToken);
        var page = await people.OrderBy(u => u.CreatedDate).ThenBy(u => u.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return ScimResult.Ok(ScimJson.List(page.Select(u => ScimJson.User(u, request.BaseUrl)), total, start));
    }

    public async Task<ScimResult> Handle(ScimUserQuery request, CancellationToken cancellationToken)
    {
        var user = await this.FindAsync(request.Caller, request.Id, cancellationToken);
        return user is null ? ScimResult.NotFound() : ScimResult.Ok(ScimJson.User(user, request.BaseUrl));
    }

    public async Task<ScimResult> Handle(CreateScimUserCommand request, CancellationToken cancellationToken)
    {
        if (request.Body is null)
        {
            return ScimResult.Error(400, "A user is a JSON object.", "invalidSyntax");
        }

        var said = ScimUserChange.FromResource(request.Body);
        if (said.UserName is null || said.UserName.Length > 256 || !EmailShape().IsMatch(said.UserName))
        {
            return ScimResult.Invalid("userName must be the person's e-mail address.");
        }

        var actor = DirectoryAccess.ActorOf(request.Caller);
        var found = await this.userAccounts.FindByUserNameAcrossTenantsAsync(this.userManager.NormalizeName(said.UserName), cancellationToken);
        if (found.Count > 0)
        {
            return await this.TakeOverAsync(request, found[0], said, actor, cancellationToken);
        }

        var user = User.Create(said.AnyName ?? said.UserName.Split('@')[0], said.UserName, said.UserName);
        user.ManageFromDirectory(request.Caller.ConnectionId, said.ExternalId);
        if (said.Active == false)
        {
            user.Suspend(TurnedOff);
        }

        try
        {
            var created = await this.userManager.CreateAsync(user);
            if (!created.Succeeded)
            {
                this.logger.LogInformation("A directory person was refused by the identity store: {Codes}.", string.Join(", ", created.Errors.Select(e => e.Code)));
                return created.Errors.Any(e => e.Code.StartsWith("Duplicate", StringComparison.Ordinal))
                    ? ScimResult.Error(409, "A person with this userName already exists.", "uniqueness")
                    : ScimResult.Invalid("Harmony could not keep this person.");
            }
        }
        catch (Exception exception) when (ScimJson.IsDuplicate(exception))
        {
            return ScimResult.Error(409, "Another person of this directory has this externalId.", "uniqueness");
        }

        await this.auditEvents.AddAsync(this.securityEvents.CreateBy(user, SecurityAuditActions.AccountCreated, actor), cancellationToken);
        await this.auditEvents.SaveChangesAsync(cancellationToken);
        await this.access.PublishAsync(user, 0, cancellationToken);
        return ScimResult.Created(ScimJson.User(user, request.BaseUrl), $"{request.BaseUrl}/Users/{user.Id:D}");
    }

    public async Task<ScimResult> Handle(ReplaceScimUserCommand request, CancellationToken cancellationToken)
        => request.Body is null
            ? ScimResult.Error(400, "A user is a JSON object.", "invalidSyntax")
            : await this.ChangeAsync(request, request.Id, ScimUserChange.FromResource(request.Body), whole: true, cancellationToken);

    public async Task<ScimResult> Handle(PatchScimUserCommand request, CancellationToken cancellationToken)
    {
        var operations = ScimPatchOperation.Read(request.Body);
        if (operations is null)
        {
            return ScimResult.Error(400, "A PATCH carries Operations with op add, replace or remove.", "invalidSyntax");
        }

        var said = new ScimUserChange();
        foreach (var operation in operations)
        {
            said.Apply(operation);
        }

        return await this.ChangeAsync(request, request.Id, said, whole: false, cancellationToken);
    }

    public async Task<ScimResult> Handle(DeleteScimUserCommand request, CancellationToken cancellationToken)
    {
        var user = await this.FindAsync(request.Caller, request.Id, cancellationToken);
        if (user is null)
        {
            return ScimResult.NotFound();
        }

        var before = user.SecurityVersion;
        user.DeleteFromDirectory(Deleted);
        foreach (var group in await this.groups.OfMemberAsync(user.Id, cancellationToken))
        {
            group.RemoveMembers([user.Id]);
        }

        await this.auditEvents.AddAsync(this.securityEvents.CreateBy(user, SecurityAuditActions.AccountTerminated, DirectoryAccess.ActorOf(request.Caller), Deleted), cancellationToken);

        // Identity's save normalises the freed user name and stores the groups and the trail row with it.
        var saved = await this.userManager.UpdateAsync(user);
        if (!saved.Succeeded)
        {
            return ScimResult.Error(500, "Harmony could not delete this person; try again.");
        }

        await this.access.PublishAsync(user, before, cancellationToken);
        return ScimResult.NoContent();
    }

    /// <summary>The same e-mail is already in Harmony: the directory takes over this customer's person, nobody else's.</summary>
    private async Task<ScimResult> TakeOverAsync(CreateScimUserCommand request, User person, ScimUserChange said, string actor, CancellationToken cancellationToken)
    {
        if (person.TenantId != request.Caller.TenantId)
        {
            return ScimResult.Error(409, "This e-mail belongs to another Harmony customer. Ask Harmony's support to move the person.", "uniqueness");
        }

        if (person.State == AdministrativeState.Terminated)
        {
            return ScimResult.Error(409, "A closed Harmony account still holds this e-mail.", "uniqueness");
        }

        if (person.ProvisioningConnectionId == request.Caller.ConnectionId)
        {
            return ScimResult.Error(409, "The person already exists.", "uniqueness");
        }

        var before = person.SecurityVersion;
        person.ManageFromDirectory(request.Caller.ConnectionId, said.ExternalId);
        if (said.AnyName is { } name && name != person.DisplayName)
        {
            person.Rename(name);
        }

        if (said.Active is { } active)
        {
            await this.SetActiveAsync(person, active, actor, cancellationToken);
        }

        await this.auditEvents.AddAsync(this.securityEvents.CreateBy(person, SecurityAuditActions.DirectoryTookOver, actor), cancellationToken);
        if (await this.SaveAsync(person) is { } refused)
        {
            return refused;
        }

        await this.access.PublishAsync(person, before, cancellationToken);
        return ScimResult.Created(ScimJson.User(person, request.BaseUrl), $"{request.BaseUrl}/Users/{person.Id:D}");
    }

    /// <summary>PUT (the whole person) and PATCH (what the operations said), the same rules.</summary>
    private async Task<ScimResult> ChangeAsync(ScimRequest request, string id, ScimUserChange said, bool whole, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var userId) || await this.userAccounts.GetWithAccessAsync(userId, cancellationToken) is not { } user
            || user.ProvisioningConnectionId != request.Caller.ConnectionId || user.State == AdministrativeState.Terminated)
        {
            return ScimResult.NotFound();
        }

        // The Mews rule: the e-mail is the person's key; re-pointing an account at another address is refused.
        if (said.UserName is not null && this.userManager.NormalizeName(said.UserName) != user.NormalizedUserName)
        {
            return ScimResult.Error(400, "userName is the person's e-mail in Harmony and cannot change. Remove the person and add them again.", "mutability");
        }

        var actor = DirectoryAccess.ActorOf(request.Caller);
        var before = user.SecurityVersion;
        if (said.ExternalIdSaid)
        {
            user.IdentifyInDirectory(said.ExternalId);
        }

        if ((whole ? said.AnyName : said.FullName) is { } name && name != user.DisplayName)
        {
            user.Rename(name);
        }

        RebuiltAccess? rebuilt = null;
        if (said.Active is { } active)
        {
            rebuilt = await this.SetActiveAsync(user, active, actor, cancellationToken);
        }

        if (await this.SaveAsync(user) is { } refused)
        {
            return refused;
        }

        await this.access.PublishAsync(user, before, cancellationToken);
        await this.access.FinishAsync([rebuilt], cancellationToken);
        return ScimResult.Ok(ScimJson.User(user, request.BaseUrl));
    }

    /// <summary>Off: suspended at once. On again: back in, with the access today's groups give (a rehire keeps the account).</summary>
    private async Task<RebuiltAccess?> SetActiveAsync(User user, bool active, string actor, CancellationToken cancellationToken)
    {
        if (!active && user.State == AdministrativeState.Active)
        {
            user.Suspend(TurnedOff);
            await this.auditEvents.AddAsync(this.securityEvents.CreateBy(user, SecurityAuditActions.AccountSuspended, actor, TurnedOff), cancellationToken);
        }
        else if (active && user.State == AdministrativeState.Suspended)
        {
            user.Reinstate();
            await this.auditEvents.AddAsync(this.securityEvents.CreateBy(user, SecurityAuditActions.AccountReinstated, actor), cancellationToken);
            if (await this.userAccounts.GetWithAccessAsync(user.Id, cancellationToken) is { } withAccess)
            {
                return await this.access.RebuildAsync(withAccess, actor, cancellationToken);
            }
        }

        return null;
    }

    private async Task<ScimResult?> SaveAsync(User user)
    {
        try
        {
            var saved = await this.userManager.UpdateAsync(user);
            return saved.Succeeded ? null : ScimResult.Invalid("Harmony could not keep this change.");
        }
        catch (Exception exception) when (ScimJson.IsDuplicate(exception))
        {
            return ScimResult.Error(409, "Another person of this directory has this externalId.", "uniqueness");
        }
    }

    /// <summary>The directory's own people, never a deleted one. The customer's wall is already on the call.</summary>
    private IQueryable<User> Visible(DirectoryCaller caller)
        => this.userAccounts.Query().Where(u => u.ProvisioningConnectionId == caller.ConnectionId && u.State != AdministrativeState.Terminated);

    private async Task<User?> FindAsync(DirectoryCaller caller, string id, CancellationToken cancellationToken)
        => Guid.TryParse(id, out var userId)
            ? await this.userManager.Users.FirstOrDefaultAsync(u => u.Id == userId && u.ProvisioningConnectionId == caller.ConnectionId && u.State != AdministrativeState.Terminated, cancellationToken)
            : null;

    // The same shape the administrator's create door accepts.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailShape();
}

/// <summary>What a SCIM body or operation says about a person; null: it did not say. Attributes Harmony does not keep are skipped.</summary>
internal sealed class ScimUserChange
{
    public string? UserName { get; private set; }

    public bool ExternalIdSaid { get; private set; }

    public string? ExternalId { get; private set; }

    public bool? Active { get; private set; }

    private string? DisplayName { get; set; }

    private string? Formatted { get; set; }

    private string? GivenName { get; set; }

    private string? FamilyName { get; set; }

    /// <summary>A whole name: displayName, the formatted name, or both parts. A PATCH of one part alone changes nothing.</summary>
    public string? FullName => this.DisplayName ?? this.Formatted
        ?? (this.GivenName is not null && this.FamilyName is not null ? $"{this.GivenName} {this.FamilyName}" : null);

    /// <summary>The best name a whole user gives.</summary>
    public string? AnyName => this.FullName ?? this.GivenName ?? this.FamilyName;

    public static ScimUserChange FromResource(JsonObject body)
    {
        var change = new ScimUserChange();
        change.Read(body);
        return change;
    }

    public void Apply(ScimPatchOperation operation)
    {
        if (operation.Path is null)
        {
            if (operation.Op != ScimPatchOperation.Remove && operation.Value is JsonObject value)
            {
                this.Read(value);
            }

            return;
        }

        if (operation.Op == ScimPatchOperation.Remove)
        {
            if (string.Equals(operation.Path, "externalId", StringComparison.OrdinalIgnoreCase))
            {
                this.ExternalIdSaid = true;
                this.ExternalId = null;
            }

            return;
        }

        this.Set(operation.Path, operation.Value);
    }

    private void Read(JsonObject body)
    {
        foreach (var (key, value) in body)
        {
            this.Set(key, value);
        }
    }

    private void Set(string attribute, JsonNode? value)
    {
        switch (attribute.ToLowerInvariant())
        {
            case "username":
                this.UserName = ScimJson.Text(value);
                break;
            case "externalid":
                this.ExternalIdSaid = true;
                this.ExternalId = ScimJson.Text(value);
                break;
            case "displayname":
                this.DisplayName = ScimJson.Text(value);
                break;
            case "active":
                this.Active = ScimJson.Flag(value);
                break;
            case "name" when value is JsonObject name:
                foreach (var (key, part) in name)
                {
                    this.Set("name." + key, part);
                }

                break;
            case "name.givenname":
                this.GivenName = ScimJson.Text(value);
                break;
            case "name.familyname":
                this.FamilyName = ScimJson.Text(value);
                break;
            case "name.formatted":
                this.Formatted = ScimJson.Text(value);
                break;
        }
    }
}
