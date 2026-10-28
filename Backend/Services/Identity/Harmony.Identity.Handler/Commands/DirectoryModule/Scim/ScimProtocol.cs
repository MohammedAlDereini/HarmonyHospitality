namespace Harmony.Identity.Handler.Commands.DirectoryModule.Scim;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;

/// <summary>What a SCIM door answers: a status, a body (or none), where a new resource lives.</summary>
public sealed record ScimResult(int Status, JsonObject? Body, string? Location = null)
{
    public static ScimResult Ok(JsonObject body) => new(200, body);

    public static ScimResult Created(JsonObject body, string location) => new(201, body, location);

    public static ScimResult NoContent() => new(204, null);

    public static ScimResult NotFound() => Error(404, "No such resource.");

    /// <summary>RFC 7644 3.12: the message says what is wrong, never how Harmony is built.</summary>
    public static ScimResult Error(int status, string detail, string? scimType = null)
    {
        var body = new JsonObject
        {
            ["schemas"] = new JsonArray(ScimJson.ErrorSchema),
            ["status"] = status.ToString(CultureInfo.InvariantCulture),
            ["detail"] = detail,
        };
        if (scimType is not null)
        {
            body["scimType"] = scimType;
        }

        return new ScimResult(status, body);
    }

    public static ScimResult Invalid(string detail) => Error(400, detail, "invalidValue");
}

/// <summary>The common parts of every SCIM request: who called and where the resources live.</summary>
public abstract class ScimRequest : BaseRequest<ScimResult>
{
    /// <summary>The directory, proven at the door; never read from the body.</summary>
    public DirectoryCaller Caller { get; set; } = null!;

    /// <summary>https://host/scim/v2, for meta.location.</summary>
    public string BaseUrl { get; set; } = null!;
}

/// <summary>SCIM 2.0's JSON (RFC 7643, RFC 7644) in the shapes Microsoft Entra ID and Okta send and read.</summary>
public static class ScimJson
{
    public const string UserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    public const string GroupSchema = "urn:ietf:params:scim:schemas:core:2.0:Group";
    public const string ListSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    public const string ErrorSchema = "urn:ietf:params:scim:api:messages:2.0:Error";
    public const string PatchSchema = "urn:ietf:params:scim:api:messages:2.0:PatchOp";

    /// <summary>A page holds 100 unless asked for fewer or more; never more than 1,000 (IPSIE SCIM profile).</summary>
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 1000;

    public static JsonObject User(User user, string baseUrl) => new()
    {
        ["schemas"] = new JsonArray(UserSchema),
        ["id"] = user.Id.ToString("D"),
        ["externalId"] = user.DirectoryExternalId,
        ["userName"] = user.UserName,
        ["displayName"] = user.DisplayName,
        ["name"] = new JsonObject { ["formatted"] = user.DisplayName },
        ["active"] = user.State == AdministrativeState.Active,
        ["emails"] = new JsonArray(new JsonObject { ["value"] = user.Email, ["type"] = "work", ["primary"] = true }),
        ["meta"] = Meta("User", user.CreatedDate, user.ModifiedDate, $"{baseUrl}/Users/{user.Id:D}"),
    };

    /// <summary>A group; members left out when the directory asked for that (excludedAttributes=members).</summary>
    public static JsonObject Group(DirectoryGroup group, IEnumerable<Guid>? members, string baseUrl)
    {
        var body = new JsonObject
        {
            ["schemas"] = new JsonArray(GroupSchema),
            ["id"] = group.Id.ToString("D"),
            ["externalId"] = group.ExternalId,
            ["displayName"] = group.DisplayName,
            ["meta"] = Meta("Group", group.CreatedDate, group.ModifiedDate, $"{baseUrl}/Groups/{group.Id:D}"),
        };
        if (members is not null)
        {
            body["members"] = new JsonArray(members.Select(id => (JsonNode)new JsonObject { ["value"] = id.ToString("D"), ["$ref"] = $"{baseUrl}/Users/{id:D}" }).ToArray());
        }

        return body;
    }

    public static JsonObject List(IEnumerable<JsonObject> resources, int totalResults, int startIndex)
    {
        var items = resources.Cast<JsonNode>().ToArray();
        return new JsonObject
        {
            ["schemas"] = new JsonArray(ListSchema),
            ["totalResults"] = totalResults,
            ["startIndex"] = startIndex,
            ["itemsPerPage"] = items.Length,
            ["Resources"] = new JsonArray(items),
        };
    }

    /// <summary>startIndex is 1-based (below 1 counts as 1); count 0 asks for the total only.</summary>
    public static (int Skip, int Take, int StartIndex) Page(int? startIndex, int? count)
    {
        var start = Math.Max(1, startIndex ?? 1);
        var take = Math.Clamp(count ?? DefaultPageSize, 0, MaxPageSize);
        return (start - 1, take, start);
    }

    /// <summary>A member of an object by name, any case (SCIM attribute names are case-insensitive).</summary>
    public static JsonNode? Get(JsonObject? body, string name)
    {
        if (body is null)
        {
            return null;
        }

        foreach (var (key, value) in body)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    public static string? Text(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? (string.IsNullOrWhiteSpace(text) ? null : text.Trim()) : null;

    /// <summary>true / false, also as text: older Entra apps send "True" and "False".</summary>
    public static bool? Flag(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<bool>(out var flag))
        {
            return flag;
        }

        return value.TryGetValue<string>(out var text) && bool.TryParse(text, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// A unique key said no. The framework's context turns the database's violation into a BusinessException "Conflict";
    /// a raw DbUpdateException is the same answer from below it.
    /// </summary>
    public static bool IsDuplicate(Exception exception)
        => exception is DbUpdateException
            || (exception is Harmony.Core.Exceptions.BusinessException business && business.Errors.Any(e => e.Code == "Conflict"));

    /// <summary>The ids in a members array: [{ "value": "id" }].</summary>
    public static IReadOnlyList<string> MemberValues(JsonNode? node)
        => node is JsonArray array
            ? array.OfType<JsonObject>().Select(m => Text(Get(m, "value"))).OfType<string>().ToList()
            : [];

    private static JsonObject Meta(string type, DateTime created, DateTime? modified, string location) => new()
    {
        ["resourceType"] = type,
        ["created"] = DateTime.SpecifyKind(created, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
        ["lastModified"] = DateTime.SpecifyKind(modified ?? created, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
        ["location"] = location,
    };
}

/// <summary>One PATCH operation (RFC 7644 3.5.2): the op in any case, an optional path, a value.</summary>
public sealed record ScimPatchOperation(string Op, string? Path, JsonNode? Value)
{
    public const string Add = "add";
    public const string Replace = "replace";
    public const string Remove = "remove";

    /// <summary>The operations of a PatchOp body; null when the body is not one.</summary>
    public static IReadOnlyList<ScimPatchOperation>? Read(JsonObject? body)
    {
        if (ScimJson.Get(body, "Operations") is not JsonArray operations)
        {
            return null;
        }

        var read = new List<ScimPatchOperation>();
        foreach (var node in operations)
        {
            var op = ScimJson.Text(ScimJson.Get(node as JsonObject, "op"))?.ToLowerInvariant();
            if (op is not (Add or Replace or Remove))
            {
                return null;
            }

            read.Add(new ScimPatchOperation(op, ScimJson.Text(ScimJson.Get((JsonObject)node!, "path")), ScimJson.Get((JsonObject)node!, "value")));
        }

        return read;
    }
}

/// <summary>
/// The filters the directories send (RFC 7644 3.4.2.2): "eq" terms joined by "and", and one bracket level, for example
/// userName eq "a@b.com", emails[type eq "work" and value eq "a@b.com"], id eq "x" and members[value eq "y"]. Each term
/// comes out as a lower-case path ("emails.value") and its value. Anything else is refused as invalidFilter.
/// </summary>
public static class ScimFilter
{
    public static bool TryParse(string? text, out IReadOnlyList<(string Path, string Value)> terms)
    {
        terms = [];
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        try
        {
            var parser = new Parser(text);
            terms = parser.All();
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed class Parser(string text)
    {
        private int at;

        public List<(string, string)> All()
        {
            var terms = this.And(null);
            this.Blanks();
            return this.at == text.Length ? terms : throw new FormatException();
        }

        private List<(string, string)> And(string? prefix)
        {
            var terms = this.Term(prefix);
            while (true)
            {
                var mark = this.at;
                if (this.Blanks() && this.Word("and") && this.Blanks())
                {
                    terms.AddRange(this.Term(prefix));
                    continue;
                }

                this.at = mark;
                return terms;
            }
        }

        private List<(string, string)> Term(string? prefix)
        {
            this.Blanks();
            var name = this.Name();
            var path = prefix is null ? name : $"{prefix}.{name}";
            if (this.at < text.Length && text[this.at] == '[')
            {
                this.at++;
                var inner = this.And(path);
                this.Blanks();
                return this.at < text.Length && text[this.at++] == ']' ? inner : throw new FormatException();
            }

            if (!(this.Blanks() && this.Word("eq") && this.Blanks()))
            {
                throw new FormatException();
            }

            return [(path.ToLowerInvariant(), this.Value())];
        }

        private string Name()
        {
            var start = this.at;
            while (this.at < text.Length && (char.IsAsciiLetterOrDigit(text[this.at]) || text[this.at] is '.' or ':' or '_' or '-' or '$'))
            {
                this.at++;
            }

            return this.at > start ? text[start..this.at] : throw new FormatException();
        }

        private string Value()
        {
            if (this.at < text.Length && text[this.at] == '"')
            {
                var start = this.at;
                this.at++;
                while (this.at < text.Length && text[this.at] != '"')
                {
                    this.at += text[this.at] == '\\' ? 2 : 1;
                }

                this.at++;
                if (this.at > text.Length)
                {
                    throw new FormatException();
                }

                return JsonSerializer.Deserialize<string>(text[start..this.at]) ?? throw new FormatException();
            }

            // true, false, null and numbers, as their text.
            var bare = new StringBuilder();
            while (this.at < text.Length && !char.IsWhiteSpace(text[this.at]) && text[this.at] != ']')
            {
                bare.Append(text[this.at++]);
            }

            return bare.Length > 0 ? bare.ToString() : throw new FormatException();
        }

        private bool Word(string word)
        {
            var end = this.at + word.Length;
            if (end <= text.Length && string.Compare(text, this.at, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (end == text.Length || !char.IsAsciiLetterOrDigit(text[end])))
            {
                this.at = end;
                return true;
            }

            return false;
        }

        private bool Blanks()
        {
            var start = this.at;
            while (this.at < text.Length && char.IsWhiteSpace(text[this.at]))
            {
                this.at++;
            }

            return this.at > start || this.at == text.Length || text[this.at] is '[' or ']' or '"';
        }
    }
}
