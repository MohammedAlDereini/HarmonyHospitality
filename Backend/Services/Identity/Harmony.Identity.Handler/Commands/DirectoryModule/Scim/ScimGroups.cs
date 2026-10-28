namespace Harmony.Identity.Handler.Commands.DirectoryModule.Scim;

using System.Text.Json.Nodes;
using Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Microsoft.Extensions.Logging;

public sealed class ScimGroupsQuery : ScimRequest
{
    public string? Filter { get; set; }

    public int? StartIndex { get; set; }

    public int? Count { get; set; }

    /// <summary>excludedAttributes=members: Entra asks for groups without their members.</summary>
    public bool WithoutMembers { get; set; }
}

public sealed class ScimGroupQuery : ScimRequest
{
    public string Id { get; set; } = null!;

    public bool WithoutMembers { get; set; }
}

public sealed class CreateScimGroupCommand : ScimRequest
{
    public JsonObject? Body { get; set; }
}

public sealed class ReplaceScimGroupCommand : ScimRequest
{
    public string Id { get; set; } = null!;

    public JsonObject? Body { get; set; }
}

public sealed class PatchScimGroupCommand : ScimRequest
{
    public string Id { get; set; } = null!;

    public JsonObject? Body { get; set; }
}

public sealed class DeleteScimGroupCommand : ScimRequest
{
    public string Id { get; set; } = null!;
}

/// <summary>
/// SCIM Groups (RFC 7644) for Microsoft Entra ID and Okta. The directory owns the name and the members; the role a group
/// gives is set in Harmony by the customer's administrator. Members are this directory's live people; an id Harmony does
/// not know is skipped (the directory's own log shows that person's failure). When a group with a role gains or loses
/// members, or is deleted, the people it touches get their access worked out again in the same save.
/// </summary>
public sealed class ScimGroupHandlers :
    IRequestHandler<ScimGroupsQuery, ScimResult>,
    IRequestHandler<ScimGroupQuery, ScimResult>,
    IRequestHandler<CreateScimGroupCommand, ScimResult>,
    IRequestHandler<ReplaceScimGroupCommand, ScimResult>,
    IRequestHandler<PatchScimGroupCommand, ScimResult>,
    IRequestHandler<DeleteScimGroupCommand, ScimResult>
{
    private readonly IDirectoryGroupRepository groups;
    private readonly IUserAccountRepository userAccounts;
    private readonly DirectoryAccess access;
    private readonly ILogger<ScimGroupHandlers> logger;

    public ScimGroupHandlers(IDirectoryGroupRepository groups, IUserAccountRepository userAccounts, DirectoryAccess access, ILogger<ScimGroupHandlers> logger)
    {
        this.groups = groups;
        this.userAccounts = userAccounts;
        this.access = access;
        this.logger = logger;
    }

    public async Task<ScimResult> Handle(ScimGroupsQuery request, CancellationToken cancellationToken)
    {
        if (!ScimFilter.TryParse(request.Filter, out var terms))
        {
            return ScimResult.Error(400, "The filter is not one Harmony reads: eq terms joined by and.", "invalidFilter");
        }

        var found = this.groups.Query().Where(g => g.ProvisioningConnectionId == request.Caller.ConnectionId);
        foreach (var (path, value) in terms)
        {
            switch (path)
            {
                case "displayname":
                    found = found.Where(g => g.DisplayName == value);
                    break;
                case "externalid":
                    found = found.Where(g => g.ExternalId == value);
                    break;
                case "id":
                    found = Guid.TryParse(value, out var id) ? found.Where(g => g.Id == id) : found.Where(_ => false);
                    break;
                case "members.value":
                case "members":
                    found = Guid.TryParse(value, out var member) ? found.Where(g => g.Members.Any(m => m.UserAccountId == member)) : found.Where(_ => false);
                    break;
                default:
                    return ScimResult.Error(400, $"Harmony cannot filter groups by {path}.", "invalidFilter");
            }
        }

        var (skip, take, start) = ScimJson.Page(request.StartIndex, request.Count);
        var total = await found.CountAsync(cancellationToken);
        var page = await (request.WithoutMembers ? found : found.Include(g => g.Members))
            .OrderBy(g => g.CreatedDate).ThenBy(g => g.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return ScimResult.Ok(ScimJson.List(page.Select(g => ScimJson.Group(g, request.WithoutMembers ? null : g.Members.Select(m => m.UserAccountId), request.BaseUrl)), total, start));
    }

    public async Task<ScimResult> Handle(ScimGroupQuery request, CancellationToken cancellationToken)
    {
        var group = await this.FindAsync(request.Caller, request.Id, cancellationToken);
        return group is null
            ? ScimResult.NotFound()
            : ScimResult.Ok(ScimJson.Group(group, request.WithoutMembers ? null : group.Members.Select(m => m.UserAccountId), request.BaseUrl));
    }

    public async Task<ScimResult> Handle(CreateScimGroupCommand request, CancellationToken cancellationToken)
    {
        var name = ScimJson.Text(ScimJson.Get(request.Body, "displayName"));
        if (name is null || name.Length > DirectoryGroup.DisplayNameMaxLength)
        {
            return ScimResult.Invalid($"displayName is required, at most {DirectoryGroup.DisplayNameMaxLength} characters.");
        }

        var connection = request.Caller.ConnectionId;
        if (await this.groups.Query().AnyAsync(g => g.ProvisioningConnectionId == connection && g.DisplayName == name, cancellationToken))
        {
            return ScimResult.Error(409, "A group with this displayName already exists.", "uniqueness");
        }

        var group = DirectoryGroup.Create(request.Caller.TenantId, connection, name, ScimJson.Text(ScimJson.Get(request.Body, "externalId")));
        group.AddMembers(await this.PeopleAsync(request.Caller, ScimJson.MemberValues(ScimJson.Get(request.Body, "members")), cancellationToken));
        await this.groups.AddAsync(group, cancellationToken);
        if (await this.SaveAsync(cancellationToken) is { } refused)
        {
            return refused;
        }

        // A new group has no role yet: nobody's access changes until the customer's administrator gives it one.
        return ScimResult.Created(ScimJson.Group(group, group.Members.Select(m => m.UserAccountId), request.BaseUrl), $"{request.BaseUrl}/Groups/{group.Id:D}");
    }

    public async Task<ScimResult> Handle(ReplaceScimGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await this.FindAsync(request.Caller, request.Id, cancellationToken);
        if (group is null)
        {
            return ScimResult.NotFound();
        }

        var name = ScimJson.Text(ScimJson.Get(request.Body, "displayName"));
        if (name is null || name.Length > DirectoryGroup.DisplayNameMaxLength)
        {
            return ScimResult.Invalid($"displayName is required, at most {DirectoryGroup.DisplayNameMaxLength} characters.");
        }

        group.Rename(name);
        group.Identify(ScimJson.Text(ScimJson.Get(request.Body, "externalId")));
        var touched = group.SetMembers(await this.PeopleAsync(request.Caller, ScimJson.MemberValues(ScimJson.Get(request.Body, "members")), cancellationToken));
        return await this.FinishAsync(request, group, touched, gone: false, ScimResult.Ok(ScimJson.Group(group, group.Members.Select(m => m.UserAccountId), request.BaseUrl)), cancellationToken);
    }

    public async Task<ScimResult> Handle(PatchScimGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await this.FindAsync(request.Caller, request.Id, cancellationToken);
        if (group is null)
        {
            return ScimResult.NotFound();
        }

        var operations = ScimPatchOperation.Read(request.Body);
        if (operations is null)
        {
            return ScimResult.Error(400, "A PATCH carries Operations with op add, replace or remove.", "invalidSyntax");
        }

        var touched = new List<Guid>();
        foreach (var operation in operations)
        {
            if (await this.ApplyAsync(request.Caller, group, operation, touched, cancellationToken) is { } refused)
            {
                return refused;
            }
        }

        return await this.FinishAsync(request, group, touched, gone: false, ScimResult.NoContent(), cancellationToken);
    }

    public async Task<ScimResult> Handle(DeleteScimGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await this.FindAsync(request.Caller, request.Id, cancellationToken);
        if (group is null)
        {
            return ScimResult.NotFound();
        }

        var touched = group.Members.Select(m => m.UserAccountId).ToList();
        await this.groups.DeleteAsync(group, cancellationToken);
        return await this.FinishAsync(request, group, touched, gone: true, ScimResult.NoContent(), cancellationToken);
    }

    /// <summary>
    /// One operation. Entra: add/remove with path "members" and a value list, replace "displayName". Okta: remove with
    /// path members[value eq "id"], and no-path replace of the whole group. Anything else Harmony does not keep is skipped.
    /// </summary>
    private async Task<ScimResult?> ApplyAsync(Harmony.Identity.Domain.Services.DirectoryCaller caller, DirectoryGroup group, ScimPatchOperation operation, List<Guid> touched, CancellationToken cancellationToken)
    {
        if (operation.Path is null)
        {
            if (operation.Value is not JsonObject value)
            {
                return operation.Op == ScimPatchOperation.Remove ? null : ScimResult.Error(400, "A PATCH without a path carries an object.", "invalidSyntax");
            }

            foreach (var (key, part) in value)
            {
                if (await this.ApplyAsync(caller, group, operation with { Path = key, Value = part }, touched, cancellationToken) is { } refused)
                {
                    return refused;
                }
            }

            return null;
        }

        var path = operation.Path.Trim();
        if (path.StartsWith("members[", StringComparison.OrdinalIgnoreCase))
        {
            if (operation.Op != ScimPatchOperation.Remove || !ScimFilter.TryParse(path, out var terms) || terms.Any(t => t.Path != "members.value"))
            {
                return ScimResult.Error(400, "Harmony reads members[value eq \"id\"] only to remove a member.", "invalidPath");
            }

            touched.AddRange(group.RemoveMembers(terms.Select(t => Guid.TryParse(t.Value, out var id) ? id : Guid.Empty)));
            return null;
        }

        switch (path.ToLowerInvariant())
        {
            case "members":
                var named = ScimJson.MemberValues(operation.Value);
                touched.AddRange(operation.Op switch
                {
                    ScimPatchOperation.Add => group.AddMembers(await this.PeopleAsync(caller, named, cancellationToken)),
                    ScimPatchOperation.Replace => group.SetMembers(await this.PeopleAsync(caller, named, cancellationToken)),
                    _ => operation.Value is null
                        ? group.SetMembers([])
                        : group.RemoveMembers(named.Select(n => Guid.TryParse(n, out var id) ? id : Guid.Empty)),
                });
                return null;
            case "displayname":
                var name = ScimJson.Text(operation.Value);
                if (operation.Op == ScimPatchOperation.Remove || name is null || name.Length > DirectoryGroup.DisplayNameMaxLength)
                {
                    return ScimResult.Error(400, "displayName is required.", "mutability");
                }

                group.Rename(name);
                return null;
            case "externalid":
                group.Identify(operation.Op == ScimPatchOperation.Remove ? null : ScimJson.Text(operation.Value));
                return null;
            default:
                return null;
        }
    }

    /// <summary>The ids that are this directory's live people; the rest are skipped and logged.</summary>
    private async Task<IReadOnlyList<Guid>> PeopleAsync(Harmony.Identity.Domain.Services.DirectoryCaller caller, IReadOnlyList<string> values, CancellationToken cancellationToken)
    {
        var ids = values.Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var known = await this.userAccounts.Query()
            .Where(u => ids.Contains(u.Id) && u.ProvisioningConnectionId == caller.ConnectionId && u.State != AdministrativeState.Terminated)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        if (known.Count < values.Count)
        {
            this.logger.LogInformation("A directory group change named {Skipped} member(s) Harmony does not have; they were skipped.", values.Count - known.Count);
        }

        return known;
    }

    /// <summary>A group with a role changed: its people get their access worked out again, saved with the group, then published.</summary>
    private async Task<ScimResult> FinishAsync(ScimRequest request, DirectoryGroup group, IReadOnlyCollection<Guid> touched, bool gone, ScimResult answer, CancellationToken cancellationToken)
    {
        var rebuilt = new List<RebuiltAccess?>();
        if (group.RoleId is not null)
        {
            foreach (var userId in touched.Distinct())
            {
                rebuilt.Add(await this.access.RebuildAsync(userId, group, gone, DirectoryAccess.ActorOf(request.Caller), cancellationToken));
            }
        }

        if (await this.SaveAsync(cancellationToken) is { } refused)
        {
            return refused;
        }

        await this.access.FinishAsync(rebuilt, cancellationToken);
        return answer;
    }

    private async Task<ScimResult?> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.groups.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (Exception exception) when (ScimJson.IsDuplicate(exception))
        {
            return ScimResult.Error(409, "Another group of this directory has this displayName or externalId.", "uniqueness");
        }
    }

    private async Task<DirectoryGroup?> FindAsync(Harmony.Identity.Domain.Services.DirectoryCaller caller, string id, CancellationToken cancellationToken)
        => Guid.TryParse(id, out var groupId) ? await this.groups.WithMembersAsync(caller.ConnectionId, groupId, cancellationToken) : null;
}
