using Harmony.Identity.Domain.Models.UserAccountModule;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Security;

/// <summary>
/// An account's security trail, newest first (the last <see cref="GetSecurityEventsQueryHandler.Take"/>). Built by the
/// controller, never bound from a request: no Id means the caller's own account.
/// </summary>
public class GetSecurityEventsQuery : BaseQueryRequest<CallResponse<List<SecurityEventModel>>>
{
    public Guid? Id { get; init; }
}
