namespace Harmony.Identity.Handler.Queries.UserAccountModule.Permissions;

/// <summary>
/// The caller's own effective permissions (the web app's session payload, D2). Built by the controller, never bound
/// from a request: there is nothing to ask for but "mine".
/// </summary>
public class GetMyPermissionsQuery : BaseQueryRequest<CallResponse<List<int>>>
{
}
